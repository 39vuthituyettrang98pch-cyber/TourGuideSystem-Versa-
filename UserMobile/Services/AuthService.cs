using System.Text.Json;
using UserMobile.Models;

namespace UserMobile.Services;

public sealed class AuthService : IAuthService
{
    private const string ProfileKey = "tourist_profile";
    private readonly IApiService _apiService;
    private readonly ILocalStorageService _storageService;
    private readonly IAccessTokenStore _tokenStore;
    private string? _validatedAccessToken;

    public AuthService(
        IApiService apiService,
        ILocalStorageService storageService,
        IAccessTokenStore tokenStore)
    {
        _apiService = apiService;
        _storageService = storageService;
        _tokenStore = tokenStore;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return Failure("Vui lòng nhập email và mật khẩu.");

        var response = await _apiService.PostAsync<AuthDto>(
            "api/auth/login",
            new { Email = email.Trim(), Password = password });

        return await SaveResultAsync(response);
    }

    public async Task<AuthResult> RegisterAsync(string fullName, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return Failure("Vui lòng nhập đầy đủ họ tên, email và mật khẩu.");
        }

        if (password.Length < 8)
            return Failure("Mật khẩu phải có ít nhất 8 ký tự.");

        var response = await _apiService.PostAsync<AuthDto>(
            "api/auth/register",
            new { FullName = fullName.Trim(), Email = email.Trim(), Password = password });

        return await SaveResultAsync(response);
    }

    public async Task LogoutAsync()
    {
        _tokenStore.Clear();
        _validatedAccessToken = null;
        await _storageService.RemoveAsync(ProfileKey);
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var accessToken = await _tokenStore.GetAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        if (IsExpired(accessToken))
        {
            await LogoutAsync();
            return false;
        }

        if (string.Equals(_validatedAccessToken, accessToken, StringComparison.Ordinal) &&
            await GetProfileAsync() is not null)
        {
            return true;
        }

        var response = await _apiService.GetAsync<UserProfile>("api/auth/me");
        if (!response.Success || response.Data is null)
        {
            await LogoutAsync();
            return false;
        }

        await _storageService.SaveAsync(ProfileKey, JsonSerializer.Serialize(response.Data));
        _validatedAccessToken = accessToken;
        return true;
    }

    public async Task<UserProfile?> GetProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(await _tokenStore.GetAsync()))
            return null;

        var json = await _storageService.GetAsync(ProfileKey);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                return JsonSerializer.Deserialize<UserProfile>(json);
            }
            catch (JsonException)
            {
                await _storageService.RemoveAsync(ProfileKey);
            }
        }

        var response = await _apiService.GetAsync<UserProfile>("api/auth/me");
        if (!response.Success || response.Data is null)
            return null;

        await _storageService.SaveAsync(ProfileKey, JsonSerializer.Serialize(response.Data));
        return response.Data;
    }

    private async Task<AuthResult> SaveResultAsync(ApiResponse<AuthDto> response)
    {
        if (!response.Success || response.Data is null)
            return Failure(response.Message);

        await _tokenStore.SetAsync(response.Data.AccessToken);
        _validatedAccessToken = response.Data.AccessToken;
        await _storageService.SaveAsync(
            ProfileKey,
            JsonSerializer.Serialize(response.Data.Profile));

        return new AuthResult
        {
            IsSuccess = true,
            Message = response.Message,
            AccessToken = response.Data.AccessToken,
            Profile = response.Data.Profile
        };
    }

    private static AuthResult Failure(string? message)
    {
        return new AuthResult
        {
            IsSuccess = false,
            Message = string.IsNullOrWhiteSpace(message)
                ? "Không thể hoàn tất yêu cầu."
                : message
        };
    }

    private static bool IsExpired(string accessToken)
    {
        try
        {
            var segments = accessToken.Split('.');
            if (segments.Length < 2)
                return true;

            var payload = segments[1]
                .Replace('-', '+')
                .Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');

            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            if (!document.RootElement.TryGetProperty("exp", out var expirationElement) ||
                !expirationElement.TryGetInt64(out var expirationUnixSeconds))
            {
                return true;
            }

            return DateTimeOffset.UtcNow >=
                   DateTimeOffset.FromUnixTimeSeconds(expirationUnixSeconds).AddSeconds(-30);
        }
        catch (Exception)
        {
            return true;
        }
    }
}
