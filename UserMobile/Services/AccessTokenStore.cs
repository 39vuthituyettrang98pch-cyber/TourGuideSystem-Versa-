namespace UserMobile.Services;

public sealed class AccessTokenStore : IAccessTokenStore
{
    private const string AccessTokenKey = "tourist_access_token";

    public Task<string?> GetAsync()
    {
        return SecureStorage.Default.GetAsync(AccessTokenKey);
    }

    public Task SetAsync(string accessToken)
    {
        return SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
    }
}
