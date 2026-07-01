using AdminWeb.Models;

namespace AdminWeb.Areas.Editor.Models;

public sealed class EditorDashboardViewModel
{
    public int TotalPois { get; set; }
    public int DraftPois { get; set; }
    public int PendingPois { get; set; }
    public int ApprovedPois { get; set; }
    public int RejectedPois { get; set; }
    public int TotalTours { get; set; }
    public int TotalCategories { get; set; }
    public int MissingTranslationPois { get; set; }
    public int MissingAudioPois { get; set; }
    public int PendingMediaTasks { get; set; }
    public int FailedMediaTasks { get; set; }
    public int AverageQualityScore { get; set; }
    public IReadOnlyList<Poi> RecentPois { get; set; } = [];
}
