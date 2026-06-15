using UserMobile.Models;

namespace UserMobile.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RegisterAsync(string fullName, string email, string password);
    Task LogoutAsync();
    Task<bool> IsLoggedInAsync();
    Task<UserProfile?> GetProfileAsync();
}
