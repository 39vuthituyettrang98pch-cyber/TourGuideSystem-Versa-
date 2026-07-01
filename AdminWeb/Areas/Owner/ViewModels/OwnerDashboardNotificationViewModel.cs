namespace AdminWeb.Areas.Owner.ViewModels;

public sealed class OwnerDashboardNotificationViewModel
{
    public string Icon { get; set; } = "fa-solid fa-bell";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Level { get; set; } = "info"; // info / success / warning / danger
    public string? ActionText { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
