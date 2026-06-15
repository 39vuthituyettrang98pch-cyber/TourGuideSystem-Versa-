using UserMobile.Models;

namespace UserMobile.Services;

public sealed class AchievementService : IAchievementService
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    public AchievementService(IApiService apiService, IAuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
    }

    public async Task<AchievementSummary> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        var response = await _apiService.GetAsync<AchievementSummary>(
            "api/achievement",
            cancellationToken);
        return ReadData(response);
    }

    public async Task<DiscoveryResult> CheckInByGpsAsync(
        string poiId,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
            throw new InvalidOperationException("Cần cấp quyền vị trí để check-in khám phá.");

        var location = await Geolocation.Default.GetLocationAsync(
            new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(15)),
            cancellationToken);
        if (location == null)
            throw new InvalidOperationException("Không lấy được vị trí hiện tại. Hãy bật GPS và thử lại.");

        return await DiscoverAsync(
            poiId,
            new
            {
                PoiId = ParsePoiId(poiId),
                Method = "GPS",
                location.Latitude,
                location.Longitude,
                AccuracyMeters = location.Accuracy
            },
            cancellationToken);
    }

    public async Task<DiscoveryResult> DiscoverByQrAsync(
        string poiId,
        string qrCodeToken,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        return await DiscoverAsync(
            poiId,
            new
            {
                PoiId = ParsePoiId(poiId),
                Method = "QR",
                QrCodeToken = qrCodeToken
            },
            cancellationToken);
    }

    private async Task<DiscoveryResult> DiscoverAsync(
        string poiId,
        object body,
        CancellationToken cancellationToken)
    {
        _ = ParsePoiId(poiId);
        var response = await _apiService.PostAsync<DiscoveryResult>(
            "api/achievement/discover",
            body,
            cancellationToken);
        return ReadData(response);
    }

    private async Task EnsureLoggedInAsync()
    {
        if (!await _authService.IsLoggedInAsync())
            throw new InvalidOperationException("Vui lòng đăng nhập để nhận điểm khám phá.");
    }

    private static int ParsePoiId(string poiId)
    {
        return int.TryParse(poiId, out var id) && id > 0
            ? id
            : throw new InvalidOperationException("POI không hợp lệ.");
    }

    private static T ReadData<T>(ApiResponse<T> response)
    {
        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data;
    }
}
