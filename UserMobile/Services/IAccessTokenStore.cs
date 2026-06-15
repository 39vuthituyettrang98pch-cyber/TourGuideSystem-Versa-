namespace UserMobile.Services;

public interface IAccessTokenStore
{
    Task<string?> GetAsync();
    Task SetAsync(string accessToken);
    void Clear();
}
