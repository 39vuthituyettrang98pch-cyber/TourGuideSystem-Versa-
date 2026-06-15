namespace UserMobile.Services;

public interface IUserPoiStateService
{
    Task<bool> IsFavoriteAsync(string poiId);
    Task<bool> ToggleFavoriteAsync(string poiId);
    Task<IReadOnlySet<string>> GetFavoriteIdsAsync();
    Task AddRecentAsync(string poiId);
    Task<IReadOnlyList<string>> GetRecentIdsAsync();
}
