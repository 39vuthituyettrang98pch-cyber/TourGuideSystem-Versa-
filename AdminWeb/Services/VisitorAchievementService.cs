using AdminWeb.Data;
using AdminWeb.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Services;

public sealed class VisitorAchievementService
{
    private readonly AppDbContext _context;

    public VisitorAchievementService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<VisitorListItemViewModel>> GetVisitorsAsync(
        string? searchString,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tourists
            .AsNoTracking()
            .Include(item => item.PoiDiscoveries)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            query = query.Where(item =>
                (item.Email != null && item.Email.Contains(searchString)) ||
                (item.FullName != null && item.FullName.Contains(searchString)));
        }

        var tourists = await query.ToListAsync(cancellationToken);
        return tourists
            .Select(item =>
            {
                var points = item.PoiDiscoveries.Sum(discovery => discovery.PointsAwarded);
                return new VisitorListItemViewModel
                {
                    Tourist = item,
                    TotalPoints = points,
                    DiscoveredPoiCount = item.PoiDiscoveries.Count,
                    RankName = GetRank(points).Name
                };
            })
            .OrderByDescending(item => item.TotalPoints)
            .ThenByDescending(item => item.Tourist.CreatedAt)
            .ToList();
    }

    public async Task<VisitorAchievementDetailsViewModel?> GetDetailsAsync(
        int touristId,
        CancellationToken cancellationToken = default)
    {
        var tourist = await _context.Tourists
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);
        if (tourist == null)
            return null;

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
        var currentRank = GetRank(totalPoints);
        var nextRank = Ranks.FirstOrDefault(item => item.MinimumPoints > totalPoints);

        return new VisitorAchievementDetailsViewModel
        {
            Tourist = tourist,
            TotalPoints = totalPoints,
            DiscoveredPoiCount = discoveredCount,
            TotalPoiCount = totalPoiCount,
            CompletionPercentage = totalPoiCount == 0
                ? 0
                : Math.Round(discoveredCount * 100d / totalPoiCount, 1),
            RankName = currentRank.Name,
            NextRankName = nextRank?.Name ?? "Đã đạt hạng cao nhất",
            PointsToNextRank = nextRank == null ? 0 : nextRank.MinimumPoints - totalPoints,
            Achievements = BuildAchievements(discoveredCount, qrCount),
            Discoveries = discoveries.Select(item => new VisitorDiscoveryViewModel
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

    private static List<VisitorAchievementBadgeViewModel> BuildAchievements(int discoveredCount, int qrCount)
    {
        return
        [
            Badge("Bước chân đầu tiên", "Khám phá POI đầu tiên.", "★", 1, discoveredCount),
            Badge("Lữ khách tò mò", "Khám phá 3 POI.", "✦", 3, discoveredCount),
            Badge("Nhà khám phá thành phố", "Khám phá 5 POI.", "◆", 5, discoveredCount),
            Badge("Người mở đường", "Khám phá 10 POI.", "▲", 10, discoveredCount),
            Badge("Thợ săn di sản", "Khám phá 20 POI.", "♛", 20, discoveredCount),
            Badge("Bậc thầy khám phá", "Khám phá 50 POI.", "✺", 50, discoveredCount),
            Badge("Thợ săn QR", "Khám phá 3 POI bằng mã QR.", "▦", 3, qrCount)
        ];
    }

    private static VisitorAchievementBadgeViewModel Badge(
        string title,
        string description,
        string icon,
        int required,
        int current)
    {
        return new VisitorAchievementBadgeViewModel
        {
            Title = title,
            Description = description,
            Icon = icon,
            RequiredValue = required,
            CurrentValue = Math.Min(current, required),
            IsUnlocked = current >= required
        };
    }

    private static RankDefinition GetRank(int points) =>
        Ranks.Last(item => points >= item.MinimumPoints);

    private static readonly RankDefinition[] Ranks =
    [
        new("Tân binh", 0),
        new("Nhà khám phá", 50),
        new("Nhà thám hiểm", 100),
        new("Người mở đường", 200),
        new("Huyền thoại địa phương", 500)
    ];

    private sealed record RankDefinition(string Name, int MinimumPoints);
}
