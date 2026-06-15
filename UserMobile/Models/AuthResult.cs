namespace UserMobile.Models;

public class AuthResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public UserProfile? Profile { get; set; }
}
