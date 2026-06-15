using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Controllers.Api;

[Route("api/achievement")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class AchievementApiController : ControllerBase
{
    private const int PointsPerPoi = 10;
    private readonly AppDbContext _context;

    public AchievementApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<AchievementSummaryDto>>> GetSummary(
        CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<AchievementSummaryDto>.Ok(
            await BuildSummaryAsync(GetTouristId(), cancellationToken)));
    }

    [HttpPost("discover")]
    public async Task<ActionResult<ApiResponse<DiscoveryResultDto>>> Discover(
        [FromBody] DiscoverPoiRequest request,
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var poi = await _context.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == request.PoiId && item.Status == "Approved",
                cancellationToken);

        if (poi == null)
            return BadRequest(ApiResponse<DiscoveryResultDto>.Fail(
                "POI không tồn tại hoặc chưa được duyệt."));

        var method = request.Method.Trim().ToUpperInvariant();
        string? validationError = method switch
        {
            "QR" => ValidateQr(poi, request.QrCodeToken),
            "GPS" => ValidateGps(poi, request),
            _ => "Phương thức khám phá không hợp lệ."
        };

        if (validationError != null)
            return BadRequest(ApiResponse<DiscoveryResultDto>.Fail(validationError));

        var existing = await _context.TouristPoiDiscoveries
            .AsNoTracking()
            .AnyAsync(
                item => item.TouristId == touristId && item.PoiId == request.PoiId,
                cancellationToken);

        if (existing)
        {
            var existingSummary = await BuildSummaryAsync(touristId, cancellationToken);
            return Ok(ApiResponse<DiscoveryResultDto>.Ok(new DiscoveryResultDto
            {
                IsNewDiscovery = false,
                Message = "Bạn đã nhận điểm khám phá tại POI này.",
                Summary = existingSummary
            }));
        }

        var before = await BuildSummaryAsync(touristId, cancellationToken);
        _context.TouristPoiDiscoveries.Add(new TouristPoiDiscovery
        {
            TouristId = touristId,
            PoiId = poi.Id,
            DiscoveryMethod = method,
            PointsAwarded = PointsPerPoi,
            VisitorLatitude = request.Latitude.HasValue ? (decimal)request.Latitude.Value : null,
            VisitorLongitude = request.Longitude.HasValue ? (decimal)request.Longitude.Value : null,
            DiscoveredAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        var summary = await BuildSummaryAsync(touristId, cancellationToken);
        var previousCodes = before.Achievements
            .Where(item => item.IsUnlocked)
            .Select(item => item.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newlyUnlocked = summary.Achievements
            .Where(item => item.IsUnlocked && !previousCodes.Contains(item.Code))
            .ToList();

        return Ok(ApiResponse<DiscoveryResultDto>.Ok(new DiscoveryResultDto
        {
            IsNewDiscovery = true,
            PointsAwarded = PointsPerPoi,
            Message = $"+{PointsPerPoi} điểm khám phá!",
            NewlyUnlockedAchievements = newlyUnlocked,
            Summary = summary
        }));
    }

    private async Task<AchievementSummaryDto> BuildSummaryAsync(
        int touristId,
        CancellationToken cancellationToken)
    {
        var discoveries = await _context.TouristPoiDiscoveries
            .AsNoTracking()
            .Where(item => item.TouristId == touristId)
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .OrderByDescending(item => item.DiscoveredAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var totalPoiCount = await _context.Pois
            .CountAsync(item => item.Status == "Approved", cancellationToken);
        var totalPoints = discoveries.Sum(item => item.PointsAwarded);
        var discoveredCount = discoveries.Count;
        var qrCount = discoveries.Count(item => item.DiscoveryMethod == "QR");
        var ranks = new[]
        {
            new RankDefinition("Tân binh", 0),
            new RankDefinition("Nhà khám phá", 50),
            new RankDefinition("Nhà thám hiểm", 100),
            new RankDefinition("Người mở đường", 200),
            new RankDefinition("Huyền thoại địa phương", 500)
        };
        var currentRankIndex = Array.FindLastIndex(ranks, item => totalPoints >= item.MinimumPoints);
        var currentRank = ranks[Math.Max(currentRankIndex, 0)];
        var nextRank = currentRankIndex + 1 < ranks.Length ? ranks[currentRankIndex + 1] : null;
        var rankProgress = nextRank == null
            ? 1
            : (double)(totalPoints - currentRank.MinimumPoints) /
              (nextRank.MinimumPoints - currentRank.MinimumPoints);

        var definitions = new[]
        {
            new AchievementDefinition("first_step", "Bước chân đầu tiên", "Khám phá POI đầu tiên.", "★", 1, discoveredCount),
            new AchievementDefinition("curious_traveler", "Lữ khách tò mò", "Khám phá 3 POI.", "✦", 3, discoveredCount),
            new AchievementDefinition("city_explorer", "Nhà khám phá thành phố", "Khám phá 5 POI.", "◆", 5, discoveredCount),
            new AchievementDefinition("trailblazer", "Người mở đường", "Khám phá 10 POI.", "▲", 10, discoveredCount),
            new AchievementDefinition("heritage_hunter", "Thợ săn di sản", "Khám phá 20 POI.", "♛", 20, discoveredCount),
            new AchievementDefinition("master_explorer", "Bậc thầy khám phá", "Khám phá 50 POI.", "✺", 50, discoveredCount),
            new AchievementDefinition("qr_hunter", "Thợ săn QR", "Khám phá 3 POI bằng mã QR.", "▦", 3, qrCount)
        };

        return new AchievementSummaryDto
        {
            TotalPoints = totalPoints,
            DiscoveredPoiCount = discoveredCount,
            TotalPoiCount = totalPoiCount,
            CompletionPercentage = totalPoiCount == 0
                ? 0
                : Math.Round(discoveredCount * 100d / totalPoiCount, 1),
            RankName = currentRank.Name,
            NextRankName = nextRank?.Name ?? "Đã đạt hạng cao nhất",
            PointsToNextRank = nextRank == null ? 0 : nextRank.MinimumPoints - totalPoints,
            RankProgress = Math.Clamp(rankProgress, 0, 1),
            Achievements = definitions.Select(definition => new AchievementDto
            {
                Code = definition.Code,
                Title = definition.Title,
                Description = definition.Description,
                Icon = definition.Icon,
                RequiredValue = definition.RequiredValue,
                CurrentValue = Math.Min(definition.CurrentValue, definition.RequiredValue),
                IsUnlocked = definition.CurrentValue >= definition.RequiredValue,
                UnlockedAt = definition.CurrentValue >= definition.RequiredValue
                    ? (definition.Code == "qr_hunter"
                        ? discoveries.Where(item => item.DiscoveryMethod == "QR")
                        : discoveries)
                        .OrderBy(item => item.DiscoveredAt)
                        .Skip(definition.RequiredValue - 1)
                        .Select(item => (DateTime?)item.DiscoveredAt)
                        .FirstOrDefault()
                    : null
            }).ToList(),
            RecentDiscoveries = discoveries.Take(5).Select(item => new RecentDiscoveryDto
            {
                PoiId = item.PoiId,
                PoiName = item.Poi?.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
                    ?? item.Poi?.Translations.FirstOrDefault()?.Name
                    ?? $"POI #{item.PoiId}",
                Method = item.DiscoveryMethod,
                Points = item.PointsAwarded,
                DiscoveredAt = item.DiscoveredAt
            }).ToList()
        };
    }

    private static string? ValidateQr(Poi poi, string? qrCodeToken)
    {
        return !string.IsNullOrWhiteSpace(qrCodeToken) &&
               string.Equals(poi.QrCodeToken, qrCodeToken.Trim(), StringComparison.Ordinal)
            ? null
            : "Mã QR không hợp lệ cho POI này.";
    }

    private static string? ValidateGps(Poi poi, DiscoverPoiRequest request)
    {
        if (!request.Latitude.HasValue || !request.Longitude.HasValue)
            return "Cần bật vị trí để check-in khám phá.";

        var distance = CalculateDistanceMeters(
            request.Latitude.Value,
            request.Longitude.Value,
            (double)poi.Latitude,
            (double)poi.Longitude);
        var accuracyAllowance = Math.Clamp(request.AccuracyMeters ?? 0, 0, 100);
        var allowedDistance = Math.Max(poi.Radius, 25) + accuracyAllowance;

        return distance <= allowedDistance
            ? null
            : $"Bạn cần đến gần POI hơn để check-in. Hiện còn cách khoảng {Math.Round(distance)} m.";
    }

    private static double CalculateDistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadius = 6_371_000;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var a = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) +
                Math.Cos(DegreesToRadians(latitude1)) *
                Math.Cos(DegreesToRadians(latitude2)) *
                Math.Sin(longitudeDelta / 2) *
                Math.Sin(longitudeDelta / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private int GetTouristId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private sealed record RankDefinition(string Name, int MinimumPoints);
    private sealed record AchievementDefinition(
        string Code,
        string Title,
        string Description,
        string Icon,
        int RequiredValue,
        int CurrentValue);
}
