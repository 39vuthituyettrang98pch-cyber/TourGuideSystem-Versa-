using AdminWeb.Models;

namespace AdminWeb.Areas.Reviewer.Models;

public sealed class ReviewerDashboardViewModel
{
    public int PendingPois { get; set; }
    public int ApprovedPois { get; set; }
    public int RejectedPois { get; set; }
    public IReadOnlyList<Poi> RecentPendingPois { get; set; } = [];
}
