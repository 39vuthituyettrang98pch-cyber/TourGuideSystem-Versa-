using UserMobile.Models;

namespace UserMobile.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RegisterAsync(string fullName, string email, string password);
    Task LogoutAsync();
    Task<bool> IsLoggedInAsync();
    Task<UserProfile?> GetProfileAsync();
    Task<ApiResponse<UserProfile>> UpdateProfileAsync(string fullName, string email);
    Task<ApiResponse<object>> ChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword);
    Task<ApiResponse<PasswordResetOtpDto>> RequestPasswordResetAsync(string email);
    Task<ApiResponse<object>> ResetPasswordAsync(
        string email,
        string otp,
        string newPassword,
        string confirmPassword);
}
