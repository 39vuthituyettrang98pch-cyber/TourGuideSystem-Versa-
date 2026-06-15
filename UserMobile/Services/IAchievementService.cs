using UserMobile.Models;

namespace UserMobile.Services;

public interface IAchievementService
{
    Task<AchievementSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<DiscoveryResult> CheckInByGpsAsync(string poiId, CancellationToken cancellationToken = default);
    Task<DiscoveryResult> DiscoverByQrAsync(
        string poiId,
        string qrCodeToken,
        CancellationToken cancellationToken = default);
}
