using System.Text.Json;

namespace UserMobile.Services;

public sealed class UserPoiStateService : IUserPoiStateService
{
    private const int RecentLimit = 30;
    private readonly ILocalStorageService _storageService;
    private readonly IAuthService _authService;
    private readonly IApiService _apiService;

    public UserPoiStateService(
        ILocalStorageService storageService,
        IAuthService authService,
        IApiService apiService)
    {
        _storageService = storageService;
        _authService = authService;
        _apiService = apiService;
    }

    public async Task<bool> IsFavoriteAsync(string poiId)
    {
        return (await GetFavoriteIdsAsync()).Contains(poiId);
    }

    public async Task<bool> ToggleFavoriteAsync(string poiId)
    {
        if (!await _authService.IsLoggedInAsync())
            throw new InvalidOperationException("Vui lòng đăng nhập để lưu địa điểm yêu thích.");

        var id = ParsePoiId(poiId);
        var isFavorite = !(await GetFavoriteIdsAsync()).Contains(poiId);
        var response = await _apiService.PostAsync<bool>(
            "api/favorite",
            new { PoiId = id, IsFavorite = isFavorite });

        if (!response.Success)
            throw new InvalidOperationException(response.Message);

        return response.Data;
    }

    public async Task<IReadOnlySet<string>> GetFavoriteIdsAsync()
    {
        if (!await _authService.IsLoggedInAsync())
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var response = await _apiService.GetAsync<List<int>>("api/favorite");
        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data
            .Select(item => item.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task AddRecentAsync(string poiId)
    {
        if (await _authService.IsLoggedInAsync())
        {
            var response = await _apiService.PostAsync<long>(
                "api/recent",
                new { PoiId = ParsePoiId(poiId) });
            if (!response.Success)
                throw new InvalidOperationException(response.Message);
            return;
        }

        var ids = (await ReadGuestRecentAsync()).ToList();
        ids.RemoveAll(id => string.Equals(id, poiId, StringComparison.OrdinalIgnoreCase));
        ids.Insert(0, poiId);
        if (ids.Count > RecentLimit)
            ids.RemoveRange(RecentLimit, ids.Count - RecentLimit);

        await _storageService.SaveAsync("recent_poi_ids_guest", JsonSerializer.Serialize(ids));
    }

    public async Task<IReadOnlyList<string>> GetRecentIdsAsync()
    {
        if (!await _authService.IsLoggedInAsync())
            return await ReadGuestRecentAsync();

        var response = await _apiService.GetAsync<List<int>>("api/recent");
        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data.Select(item => item.ToString()).ToList();
    }

    private static int ParsePoiId(string poiId)
    {
        if (!int.TryParse(poiId, out var id) || id <= 0)
            throw new InvalidOperationException("POI không hợp lệ.");

        return id;
    }

    private async Task<IReadOnlyList<string>> ReadGuestRecentAsync()
    {
        var json = await _storageService.GetAsync("recent_poi_ids_guest");
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
