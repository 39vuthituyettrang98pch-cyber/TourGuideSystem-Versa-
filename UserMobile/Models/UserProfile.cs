namespace UserMobile.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Email : FullName;
}
