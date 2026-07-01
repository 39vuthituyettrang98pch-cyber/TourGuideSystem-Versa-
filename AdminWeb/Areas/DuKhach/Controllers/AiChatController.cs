using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
[Route("DuKhach/AiChat")]
[EnableRateLimiting("AiPerIp")]
public sealed class AiChatController : Controller
{
    private readonly AppDbContext _context;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<AiChatController> _logger;

    public AiChatController(
        AppDbContext context,
        IGeminiService geminiService,
        ILogger<AiChatController> logger)
    {
        _context = context;
        _geminiService = geminiService;
        _logger = logger;
    }

    [HttpPost("Ask")]
    public async Task<IActionResult> Ask([FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        var question = NormalizeQuestion(request.Message);
        if (string.IsNullOrWhiteSpace(question))
        {
            return BadRequest(new AiChatResponse
            {
                Reply = "Bạn hãy nhập câu hỏi về điểm tham quan, tour, bản đồ hoặc thuyết minh nhé."
            });
        }

        var touristId = GetTouristId();
        if (touristId == null)
        {
            return Json(new AiChatResponse
            {
                Reply = "AI hướng dẫn viên là tính năng Premium. Bạn hãy đăng nhập và mua gói Premium để sử dụng."
            });
        }

        if (!await HasActivePremiumAsync(touristId.Value, cancellationToken))
        {
            return Json(new AiChatResponse
            {
                Reply = "AI hướng dẫn viên là tính năng Premium. Vui lòng mua gói Du khách Premium để hỏi AI, mở tour premium và nghe thuyết minh không giới hạn."
            });
        }

        var languageCode = NormalizeLanguageCode(request.LanguageCode);
        var contextText = await BuildKnowledgeContextAsync(languageCode, cancellationToken);

        try
        {
            var prompt = BuildPrompt(question, languageCode, contextText, request.CurrentPath);
            var aiReply = await _geminiService.GenerateTextAsync(prompt, cancellationToken);

            return Json(new AiChatResponse
            {
                Reply = string.IsNullOrWhiteSpace(aiReply)
                    ? BuildFallbackReply(languageCode, contextText)
                    : aiReply.Trim()
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không gọi được Gemini cho chatbox du khách.");

            return Json(new AiChatResponse
            {
                Reply = BuildFallbackReply(languageCode, contextText)
            });
        }
    }

    private int? GetTouristId()
    {
        var touristIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(touristIdText, out var touristId) ? touristId : null;
    }

    private async Task<bool> HasActivePremiumAsync(int touristId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.TouristSubscriptions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .AnyAsync(item =>
                item.TouristId == touristId &&
                item.Status == "Active" &&
                item.ExpiresAt > now &&
                item.PaymentPlan != null &&
                (item.PaymentPlan.PlanCode == "USER_PREMIUM" || item.PaymentPlan.Audience == "Tourist" || item.PaymentPlan.Audience == "Both"),
                cancellationToken);
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
            .Take(30)
            .ToListAsync(cancellationToken);

        var tours = await _context.Tours
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.TourPois)
            .Where(item => item.Status == "active")
            .OrderBy(item => item.Id)
            .Take(12)
            .ToListAsync(cancellationToken);

        var categories = await _context.Categories
            .AsNoTracking()
            .Include(item => item.Translations)
            .Where(item => item.Status == "active")
            .OrderBy(item => item.Id)
            .Take(15)
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
            sb.AppendLine($"  Mô tả: {TrimText(description, 260)}");
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
            sb.AppendLine($"  Mô tả: {TrimText(t.Description, 260)}");
        }

        return sb.ToString();
    }

    private static string BuildPrompt(string question, string languageCode, string contextText, string? currentPath)
    {
        return $$"""
Bạn là chatbox AI của web du khách VERSA Travel tại TP.HCM/Sài Gòn.

Nhiệm vụ:
- Trả lời câu hỏi của du khách về điểm tham quan, tour, bản đồ, chọn ngôn ngữ và thuyết minh.
- Ưu tiên dùng dữ liệu hệ thống bên dưới, không bịa địa điểm không có trong dữ liệu.
- Nếu người dùng hỏi nên đi đâu, hãy gợi ý 3-5 điểm hoặc tour phù hợp.
- Nếu người dùng hỏi cách nghe thuyết minh, hướng dẫn họ chọn ngôn ngữ rồi bấm POI trên bản đồ để nghe.
- Nếu dữ liệu chưa có thông tin, nói rõ là hệ thống chưa có dữ liệu đó.
- Trả lời bằng ngôn ngữ tương ứng với mã ngôn ngữ đang chọn nếu có thể. Nếu không chắc, trả lời tiếng Việt.
- Trả lời ngắn gọn, thân thiện, dễ hiểu. Không dùng markdown bảng.

Mã ngôn ngữ đang chọn: {{languageCode}}
Trang hiện tại: {{currentPath ?? "/DuKhach"}}

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
                : "Chatbox đã được bật, nhưng hệ thống chưa có dữ liệu POI để gợi ý. Bạn hãy thêm POI trong admin trước nhé.";
        }

        return intro + "\n" + string.Join("\n", names.Select(name => "• " + name));
    }

    private static string NormalizeQuestion(string? message)
    {
        var text = (message ?? string.Empty).Trim();
        text = Regex.Replace(text, @"\s+", " ");
        return text.Length > 700 ? text[..700] : text;
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        var code = (languageCode ?? "vi").Trim().ToLowerInvariant();
        return Regex.IsMatch(code, "^[a-z]{2,3}(-[a-z]{2})?$") ? code : "vi";
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

    public sealed class AiChatRequest
    {
        public string? Message { get; set; }
        public string? LanguageCode { get; set; }
        public string? CurrentPath { get; set; }
    }

    public sealed class AiChatResponse
    {
        public string Reply { get; set; } = "";
    }
}
