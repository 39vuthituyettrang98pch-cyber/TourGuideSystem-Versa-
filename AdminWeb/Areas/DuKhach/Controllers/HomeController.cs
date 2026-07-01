using AdminWeb.Areas.DuKhach.Models;
using AdminWeb.Data;
using AdminWeb.Services;
using AdminWeb.Services.Payments;
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

        var featuredOwnerIds = await GetFeaturedOwnerIdsAsync(cancellationToken);
        var featuredPoiIds = await GetFeaturedPoiIdsAsync(featuredOwnerIds, cancellationToken);

        var approvedPoiCount = await _context.Pois
            .CountAsync(item => item.Status == "Approved", cancellationToken);
        var activeTourCount = await _context.Tours
            .CountAsync(item => item.Status == "active", cancellationToken);

        var featuredPoiRows = await _context.Pois
            .AsNoTracking()
            .Where(item =>
                item.Status == "Approved" &&
                item.Latitude >= -90 && item.Latitude <= 90 &&
                item.Longitude >= -180 && item.Longitude <= 180)
            .Select(item => new
            {
                item.Id,
                item.CoverImageUrl,
                item.Latitude,
                item.Longitude,
                item.CreatedAt,
                item.OwnerProfileId,
                OwnerBusinessName = item.OwnerProfile == null ? null : item.OwnerProfile.BusinessName
            })
            .ToListAsync(cancellationToken);

        var featuredPois = featuredPoiRows
            .OrderByDescending(item => IsFeaturedPoi(item.Id, item.OwnerProfileId, featuredOwnerIds, featuredPoiIds))
            .ThenByDescending(item => item.CreatedAt)
            .Take(6)
            .ToList();

        var featuredPoiIdsForTranslations = featuredPois.Select(item => item.Id).ToList();
        var featuredPoiTranslations = await _context.PoiTranslations
            .AsNoTracking()
            .Where(item => featuredPoiIdsForTranslations.Contains(item.PoiId))
            .ToListAsync(cancellationToken);
        var translationsByPoiId = featuredPoiTranslations
            .GroupBy(item => item.PoiId)
            .ToDictionary(item => item.Key, item => item.ToList());

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
                translationsByPoiId.TryGetValue(item.Id, out var translations);
                translations ??= [];

                var translation = translations.FirstOrDefault(t => t.LanguageCode == selectedLanguageCode)
                    ?? translations.FirstOrDefault(t => t.LanguageCode == "vi")
                    ?? translations.FirstOrDefault(t => t.LanguageCode == "en")
                    ?? translations.FirstOrDefault();
                return new DuKhachPoiCardViewModel
                {
                    Id = item.Id,
                    Name = translation?.Name ?? $"POI #{item.Id}",
                    ShortDescription = translation?.ShortDescription ?? "Khám phá điểm tham quan này trên bản đồ.",
                    CoverImageUrl = item.CoverImageUrl,
                    Latitude = (double)item.Latitude,
                    Longitude = (double)item.Longitude,
                    IsFeatured = IsFeaturedPoi(item.Id, item.OwnerProfileId, featuredOwnerIds, featuredPoiIds),
                    OwnerBusinessName = item.OwnerBusinessName
                };
            }).ToList()
        };

        return View(model);
    }

    private async Task<HashSet<int>> GetFeaturedOwnerIdsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var activeSubscriptions = await _context.OwnerSubscriptions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .Where(item =>
                item.Status == "Active" &&
                item.StartsAt <= now &&
                item.ExpiresAt > now &&
                item.PaymentPlan != null &&
                (item.PaymentPlan.Audience == "Owner" || item.PaymentPlan.Audience == "Both"))
            .ToListAsync(cancellationToken);

        var ownerIds = activeSubscriptions
            .Where(item => OwnerFeaturedPlanHelper.IsFeaturedMapPlan(item.PaymentPlan))
            .Select(item => item.OwnerProfileId)
            .ToHashSet();

        var paidTransactions = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .Where(item =>
                item.PayerType == "Owner" &&
                item.OwnerProfileId.HasValue &&
                item.Status == "Paid" &&
                item.PaymentPlan != null &&
                (item.PaymentPlan.Audience == "Owner" || item.PaymentPlan.Audience == "Both"))
            .ToListAsync(cancellationToken);

        foreach (var payment in paidTransactions.Where(payment =>
            payment.OwnerProfileId.HasValue &&
            OwnerFeaturedPlanHelper.IsFeaturedMapPlan(payment.PaymentPlan) &&
            OwnerFeaturedPlanHelper.IsPaymentStillActive(payment)))
        {
            ownerIds.Add(payment.OwnerProfileId!.Value);
        }

        return ownerIds;
    }

    private async Task<HashSet<int>> GetFeaturedPoiIdsAsync(HashSet<int> featuredOwnerIds, CancellationToken cancellationToken)
    {
        if (featuredOwnerIds.Count == 0)
            return [];

        var fromMenuItems = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => featuredOwnerIds.Contains(item.OwnerProfileId) && item.Status != "Hidden")
            .Select(item => item.PoiId)
            .ToListAsync(cancellationToken);

        var fromApprovedOwnerRequests = await _context.PoiOwnerRequests
            .AsNoTracking()
            .Where(item =>
                item.PoiId.HasValue &&
                featuredOwnerIds.Contains(item.OwnerProfileId) &&
                item.Status == "Approved")
            .Select(item => item.PoiId!.Value)
            .ToListAsync(cancellationToken);

        return fromMenuItems
            .Concat(fromApprovedOwnerRequests)
            .ToHashSet();
    }

    private static bool IsFeaturedPoi(AdminWeb.Models.Poi poi, HashSet<int> featuredOwnerIds, HashSet<int> featuredPoiIds)
    {
        return (poi.OwnerProfileId.HasValue && featuredOwnerIds.Contains(poi.OwnerProfileId.Value))
            || featuredPoiIds.Contains(poi.Id);
    }

    private static bool IsFeaturedPoi(int poiId, int? ownerProfileId, HashSet<int> featuredOwnerIds, HashSet<int> featuredPoiIds)
    {
        return (ownerProfileId.HasValue && featuredOwnerIds.Contains(ownerProfileId.Value))
            || featuredPoiIds.Contains(poiId);
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
