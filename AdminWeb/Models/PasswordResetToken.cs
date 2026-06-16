namespace AdminWeb.Models;

public sealed class PasswordResetToken
{
    public long Id { get; set; }
    public int TouristId { get; set; }
    public string Email { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public Tourist? Tourist { get; set; }
}
