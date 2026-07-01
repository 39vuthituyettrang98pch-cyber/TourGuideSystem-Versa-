using System.Security.Claims;
using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers.Api;

[ApiController]
[Route("api/leaderboard")]
public sealed class LeaderboardApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public LeaderboardApiController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaderboardItemDto>>>> Get(
        CancellationToken cancellationToken)
    {
        var currentId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
        var stats = await _context.TouristPoiDiscoveries
            .AsNoTracking()
            .GroupBy(item => item.TouristId)
            .Select(group => new
            {
                TouristId = group.Key,
                TotalPoints = group.Sum(item => item.PointsAwarded),
                DiscoveredPoiCount = group.Select(item => item.PoiId).Distinct().Count()
            })
            .OrderByDescending(item => item.TotalPoints)
            .ThenByDescending(item => item.DiscoveredPoiCount)
            .Take(50)
            .ToListAsync(cancellationToken);

        var ids = stats.Select(item => item.TouristId).ToList();
        var names = await _context.Tourists.AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.FullName, cancellationToken);

        var result = stats.Select((item, index) =>
        {
            var displayName = names.TryGetValue(item.TouristId, out var fullName) && !string.IsNullOrWhiteSpace(fullName)
                ? fullName!
                : $"Du khách #{item.TouristId}";

            return new LeaderboardItemDto
            {
                Rank = index + 1,
                DisplayName = displayName,
                TotalPoints = item.TotalPoints,
                DiscoveredPoiCount = item.DiscoveredPoiCount,
                RankName = RankName(item.TotalPoints),
                IsCurrentUser = currentId == item.TouristId
            };
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<LeaderboardItemDto>>.Ok(result));
    }

    private static string RankName(int points) => points switch
    {
        >= 500 => "Huyền thoại địa phương",
        >= 200 => "Người mở đường",
        >= 100 => "Nhà thám hiểm",
        >= 50 => "Nhà khám phá",
        _ => "Tân binh"
    };
}
