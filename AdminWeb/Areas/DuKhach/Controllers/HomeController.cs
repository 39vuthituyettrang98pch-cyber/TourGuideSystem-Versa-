using AdminWeb.Areas.DuKhach.Models;
using AdminWeb.Data;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
[AllowAnonymous]
public sealed class HomeController : Controller
{
    private const string FallbackLanguageCode = "vi";

    private readonly AppDbContext _context;
    private readonly VisitorAchievementService _achievementService;

    public HomeController(AppDbContext context, VisitorAchievementService achievementService)
    {
        _context = context;
        _achievementService = achievementService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var selectedLanguageCode = GetSelectedLanguageCode();
        ViewBag.SelectedLanguageCode = selectedLanguageCode;

        var approvedPoiCount = await _context.Pois
            .CountAsync(item => item.Status == "Approved", cancellationToken);
        var activeTourCount = await _context.Tours
            .CountAsync(item => item.Status == "active", cancellationToken);

        var featuredPois = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .Where(item =>
                item.Status == "Approved" &&
                item.Latitude >= -90 && item.Latitude <= 90 &&
                item.Longitude >= -180 && item.Longitude <= 180)
            .OrderByDescending(item => item.CreatedAt)
            .Take(6)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var discoveredPoiCount = 0;
        var totalPoints = 0;
        var rankName = "Tân binh";
        IReadOnlyList<DuKhachRecentDiscoveryViewModel> recentDiscoveries = [];

        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Tourist"))
        {
            var touristId = GetTouristId();
            var details = await _achievementService.GetDetailsAsync(touristId, cancellationToken);
            if (details != null)
            {
                discoveredPoiCount = details.DiscoveredPoiCount;
                totalPoints = details.TotalPoints;
                rankName = details.RankName;
                recentDiscoveries = details.Discoveries.Take(5).Select(item => new DuKhachRecentDiscoveryViewModel
                {
                    PoiId = item.PoiId,
                    PoiName = item.PoiName,
                    Method = item.Method,
                    Points = item.Points,
                    DiscoveredAt = item.DiscoveredAt
                }).ToList();
            }
        }

        var model = new DuKhachDashboardViewModel
        {
            ApprovedPoiCount = approvedPoiCount,
            TourCount = activeTourCount,
            DiscoveredPoiCount = discoveredPoiCount,
            TotalPoints = totalPoints,
            RankName = rankName,
            RecentDiscoveries = recentDiscoveries,
            FeaturedPois = featuredPois.Select(item =>
            {
                var translation = item.Translations.FirstOrDefault(t => t.LanguageCode == selectedLanguageCode)
                    ?? item.Translations.FirstOrDefault(t => t.LanguageCode == "vi")
                    ?? item.Translations.FirstOrDefault(t => t.LanguageCode == "en")
                    ?? item.Translations.FirstOrDefault();
                return new DuKhachPoiCardViewModel
                {
                    Id = item.Id,
                    Name = translation?.Name ?? $"POI #{item.Id}",
                    ShortDescription = translation?.ShortDescription ?? "Khám phá điểm tham quan này trên bản đồ.",
                    CoverImageUrl = item.CoverImageUrl,
                    Latitude = (double)item.Latitude,
                    Longitude = (double)item.Longitude
                };
            }).ToList()
        };

        return View(model);
    }

    private int GetTouristId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string GetSelectedLanguageCode()
    {
        var raw = Request.Query["lang"].FirstOrDefault()
            ?? Request.Cookies["versa.dukhach.lang"]
            ?? "vi";

        var languageCode = NormalizeLanguageCode(raw);
        Response.Cookies.Append("versa.dukhach.lang", languageCode, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });

        return languageCode;
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        var normalized = (languageCode ?? FallbackLanguageCode).Trim().ToLowerInvariant();
        return normalized.Length is >= 2 and <= 10 &&
            normalized.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
                ? normalized
                : FallbackLanguageCode;
    }

    [HttpPost]
    public async Task<IActionResult> AskAI([FromBody] DuKhachChatRequest request, [FromServices] IGeminiService geminiService)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Json(new { success = false, message = "Vui lòng nhập câu hỏi." });

        try
        {
            var prompt = $"Bạn là trợ lý ảo AI về du lịch của hệ thống VERSA. Hãy trả lời câu hỏi sau của du khách một cách ngắn gọn, thân thiện và hữu ích nhất: {request.Message}";
            
            var responseText = await geminiService.GenerateTextAsync(prompt);
            return Json(new { success = true, reply = responseText });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "Xin lỗi, trợ lý AI đang quá tải. Vui lòng thử lại sau nhé." });
        }
    }
}
