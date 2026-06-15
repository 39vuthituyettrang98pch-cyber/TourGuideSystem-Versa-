namespace UserMobile.Models;

public sealed class AuthDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public UserProfile Profile { get; set; } = new();
}
