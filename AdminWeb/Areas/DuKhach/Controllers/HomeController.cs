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
    private readonly AppDbContext _context;
    private readonly VisitorAchievementService _achievementService;

    public HomeController(AppDbContext context, VisitorAchievementService achievementService)
    {
        _context = context;
        _achievementService = achievementService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
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
                var translation = item.Translations.FirstOrDefault(t => t.LanguageCode == "vi")
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
}
