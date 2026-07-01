using UserMobile.Models;

namespace UserMobile.Services;

public interface IAchievementService
{
    Task<AchievementSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<DiscoveryResult> CheckInByGpsAsync(string poiId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardItem>> GetLeaderboardAsync(CancellationToken cancellationToken = default);
}
