namespace UserMobile.Services;

public interface ILocalStorageService
{
    Task SaveAsync(string key, string value);
    Task<string?> GetAsync(string key);
    Task RemoveAsync(string key);
}
