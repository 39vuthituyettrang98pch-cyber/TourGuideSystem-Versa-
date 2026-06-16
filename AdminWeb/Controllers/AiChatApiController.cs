using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace AdminWeb.Controllers.Api;

[ApiController]
[Route("api/ai-chat")]
public sealed class AiChatApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<AiChatApiController> _logger;

    public AiChatApiController(
        AppDbContext context,
        IGeminiService geminiService,
        ILogger<AiChatApiController> logger)
    {
        _context = context;
        _geminiService = geminiService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AiChatReplyDto>>> Ask(
        [FromBody] AiChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var question = NormalizeQuestion(request.Message);
        if (string.IsNullOrWhiteSpace(question))
        {
            return BadRequest(ApiResponse<AiChatReplyDto>.Fail(
                "Bạn hãy nhập câu hỏi về điểm tham quan, tour, bản đồ hoặc thuyết minh."));
        }

        var languageCode = await NormalizeLanguageCodeAsync(request.LanguageCode, cancellationToken);
        var contextText = await BuildKnowledgeContextAsync(languageCode, cancellationToken);

        try
        {
            var prompt = BuildPrompt(question, languageCode, contextText, request.CurrentScreen);
            var aiReply = await _geminiService.GenerateTextAsync(prompt, cancellationToken);
            var reply = string.IsNullOrWhiteSpace(aiReply)
                ? BuildFallbackReply(languageCode, contextText)
                : aiReply.Trim();

            return Ok(ApiResponse<AiChatReplyDto>.Ok(
                new AiChatReplyDto
                {
                    Reply = reply,
                    LanguageCode = languageCode,
                    Source = "gemini"
                },
                "AI đã trả lời."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không gọi được Gemini cho app AI chat.");

            return Ok(ApiResponse<AiChatReplyDto>.Ok(
                new AiChatReplyDto
                {
                    Reply = BuildFallbackReply(languageCode, contextText),
                    LanguageCode = languageCode,
                    Source = "fallback"
                },
                "AI fallback từ dữ liệu hệ thống."));
        }
    }

    private async Task<string> NormalizeLanguageCodeAsync(string? languageCode, CancellationToken cancellationToken)
    {
        var code = (languageCode ?? "vi").Trim().ToLowerInvariant();
        if (!Regex.IsMatch(code, "^[a-z]{2,3}(-[a-z]{2})?$"))
            code = "vi";

        var exists = await _context.SupportedLanguages
            .AsNoTracking()
            .AnyAsync(language => language.IsActive && language.LanguageCode.ToLower() == code, cancellationToken);

        return exists ? code : "vi";
    }

    private async Task<string> BuildKnowledgeContextAsync(string languageCode, CancellationToken cancellationToken)
    {
        const string fallbackLanguage = "vi";

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.PoiCategories)
                .ThenInclude(item => item.Category)
                    .ThenInclude(item => item!.Translations)
            .Where(item => item.Status == "Approved")
            .OrderByDescending(item => item.CreatedAt)
            .Take(35)
            .ToListAsync(cancellationToken);

        var tours = await _context.Tours
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.TourPois)
            .Where(item => item.Status == "active")
            .OrderBy(item => item.Id)
            .Take(15)
            .ToListAsync(cancellationToken);

        var categories = await _context.Categories
            .AsNoTracking()
            .Include(item => item.Translations)
            .Where(item => item.Status == "active")
            .OrderBy(item => item.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("DỮ LIỆU HỆ THỐNG VERSA TRAVEL:");
        sb.AppendLine();
        sb.AppendLine("NGÔN NGỮ ĐANG CHỌN:");
        sb.AppendLine(languageCode);
        sb.AppendLine();

        sb.AppendLine("DANH MỤC:");
        foreach (var category in categories)
        {
            var translation = FindCategoryTranslation(category, languageCode, fallbackLanguage);
            if (!string.IsNullOrWhiteSpace(translation))
                sb.AppendLine("- " + translation);
        }
        sb.AppendLine();

        sb.AppendLine("ĐIỂM THAM QUAN:");
        foreach (var poi in pois)
        {
            var t = FindPoiTranslation(poi, languageCode, fallbackLanguage);
            if (t == null || string.IsNullOrWhiteSpace(t.Name))
                continue;

            var description = FirstNonEmpty(t.ShortDescription, t.FullDescription, "Chưa có mô tả.");
            sb.AppendLine($"- POI #{poi.Id}: {t.Name}");
            sb.AppendLine($"  Mô tả: {TrimText(description, 280)}");
            sb.AppendLine($"  Vị trí: {poi.Latitude}, {poi.Longitude}; bán kính gợi ý: {poi.Radius}m");

            var categoryNames = poi.PoiCategories
                .Select(item => item.Category)
                .Where(item => item != null)
                .Select(item => FindCategoryTranslation(item!, languageCode, fallbackLanguage))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct()
                .Take(4)
                .ToList();

            if (categoryNames.Count > 0)
                sb.AppendLine($"  Danh mục: {string.Join(", ", categoryNames)}");
        }
        sb.AppendLine();

        sb.AppendLine("TOUR DU LỊCH:");
        foreach (var tour in tours)
        {
            var t = FindTourTranslation(tour, languageCode, fallbackLanguage);
            if (t == null || string.IsNullOrWhiteSpace(t.Title))
                continue;

            sb.AppendLine($"- Tour #{tour.Id}: {t.Title}");
            sb.AppendLine($"  Thời lượng: khoảng {tour.EstimatedTime} phút; số điểm: {tour.TourPois.Count}");
            sb.AppendLine($"  Mô tả: {TrimText(t.Description, 280)}");
        }

        return sb.ToString();
    }

    private static string BuildPrompt(string question, string languageCode, string contextText, string? currentScreen)
    {
        return $$"""
Bạn là trợ lý AI trong app mobile VERSA Travel tại TP.HCM/Sài Gòn.

Nhiệm vụ:
- Trả lời câu hỏi của du khách về điểm tham quan, tour, bản đồ, QR, yêu thích, đánh giá, chọn ngôn ngữ và thuyết minh.
- Ưu tiên dùng dữ liệu hệ thống bên dưới, không bịa địa điểm không có trong dữ liệu.
- Nếu người dùng hỏi nên đi đâu, hãy gợi ý 3-5 điểm hoặc tour phù hợp.
- Nếu người dùng hỏi cách nghe thuyết minh, hướng dẫn họ chọn ngôn ngữ rồi mở POI trên bản đồ/QR để nghe.
- Nếu dữ liệu chưa có thông tin, nói rõ là hệ thống chưa có dữ liệu đó.
- Trả lời bằng ngôn ngữ tương ứng với mã ngôn ngữ đang chọn nếu có thể. Nếu không chắc, trả lời tiếng Việt.
- Trả lời ngắn gọn, thân thiện, dễ hiểu, phù hợp màn hình điện thoại. Không dùng markdown bảng.

Mã ngôn ngữ đang chọn: {{languageCode}}
Màn hình hiện tại trong app: {{currentScreen ?? "App mobile"}}

{{contextText}}

Câu hỏi của du khách:
{{question}}
""";
    }

    private static PoiTranslation? FindPoiTranslation(Poi poi, string languageCode, string fallbackLanguage)
    {
        return poi.Translations.FirstOrDefault(item => item.LanguageCode == languageCode)
            ?? poi.Translations.FirstOrDefault(item => item.LanguageCode == fallbackLanguage)
            ?? poi.Translations.FirstOrDefault();
    }

    private static TourTranslation? FindTourTranslation(Tour tour, string languageCode, string fallbackLanguage)
    {
        return tour.Translations.FirstOrDefault(item => item.LanguageCode == languageCode)
            ?? tour.Translations.FirstOrDefault(item => item.LanguageCode == fallbackLanguage)
            ?? tour.Translations.FirstOrDefault();
    }

    private static string? FindCategoryTranslation(Category category, string languageCode, string fallbackLanguage)
    {
        return category.Translations.FirstOrDefault(item => item.LanguageCode == languageCode)?.Name
            ?? category.Translations.FirstOrDefault(item => item.LanguageCode == fallbackLanguage)?.Name
            ?? category.Translations.FirstOrDefault()?.Name;
    }

    private static string BuildFallbackReply(string languageCode, string contextText)
    {
        var names = Regex.Matches(contextText, @"- POI #\d+: (?<name>.+)")
            .Select(m => m.Groups["name"].Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(5)
            .ToList();

        var intro = languageCode.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? "AI is not configured yet, but I can suggest these places from the system:"
            : "AI chưa cấu hình hoặc đang lỗi, nhưng mình có thể gợi ý nhanh các điểm đang có trong hệ thống:";

        if (names.Count == 0)
        {
            return languageCode.Equals("en", StringComparison.OrdinalIgnoreCase)
                ? "AI chat is available, but the system has no POI data to suggest yet. Please add POI data in admin first."
                : "AI chat đã được bật, nhưng hệ thống chưa có dữ liệu POI để gợi ý. Bạn hãy thêm POI trong admin trước nhé.";
        }

        return intro + "\n" + string.Join("\n", names.Select(name => "• " + name));
    }

    private static string NormalizeQuestion(string? message)
    {
        var text = (message ?? string.Empty).Trim();
        text = Regex.Replace(text, @"\s+", " ");
        return text.Length > 700 ? text[..700] : text;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
    }

    private static string TrimText(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Chưa có mô tả.";

        var clean = Regex.Replace(text.Trim(), @"\s+", " ");
        return clean.Length <= maxLength ? clean : clean[..maxLength].TrimEnd() + "...";
    }
}
