using AdminWeb.Areas.DuKhach.Models;
using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
[AllowAnonymous]
public sealed class LeaderboardController : Controller
{
    private readonly AppDbContext _context;

    public LeaderboardController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Group logs by tourist to calculate stats efficiently
        var touristStats = await _context.TouristPoiDiscoveries
            .AsNoTracking()
            .GroupBy(log => log.TouristId)
            .Select(g => new
            {
                TouristId = g.Key,
                TotalPoints = g.Sum(l => l.PointsAwarded),
                DiscoveredCount = g.Select(l => l.PoiId).Distinct().Count()
            })
            .OrderByDescending(s => s.TotalPoints)
            .ThenByDescending(s => s.DiscoveredCount)
            .Take(50)
            .ToListAsync(cancellationToken);

        // Get the tourist details for the top 50
        var touristIds = touristStats.Select(s => s.TouristId).ToList();
        var tourists = await _context.Tourists
            .AsNoTracking()
            .Where(t => touristIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t, cancellationToken);

        // Map the stats and tourist info to the view model
        var leaderboard = touristStats.Select((stat, index) =>
        {
            if (!tourists.TryGetValue(stat.TouristId, out var tourist))
            {
                return null; // Skip if tourist not found
            }

            // Determine rank name based on points
            var rankName = "Tân binh";
            if (stat.TotalPoints >= 500) rankName = "Bậc thầy";
            else if (stat.TotalPoints >= 200) rankName = "Nhà thám hiểm";
            else if (stat.TotalPoints >= 50) rankName = "Người khám phá";

            return new DuKhachLeaderboardItemViewModel
            {
                Rank = index + 1,
                FullName = string.IsNullOrWhiteSpace(tourist.FullName) ? $"Du khách #{tourist.Id}" : tourist.FullName,
                TotalPoints = stat.TotalPoints,
                DiscoveredCount = stat.DiscoveredCount,
                RankName = rankName
            };
        }).Where(item => item != null).ToList();

        return View(leaderboard!);
    }
}