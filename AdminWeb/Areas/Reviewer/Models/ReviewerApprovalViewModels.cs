using AdminWeb.Models;

namespace AdminWeb.Areas.Reviewer.Models;

public sealed class ReviewerApprovalQueueViewModel
{
    public string StatusFilter { get; set; } = "Pending";
    public string? Search { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int StalePendingCount { get; set; }
    public IReadOnlyList<ReviewerPoiReviewItemViewModel> Items { get; set; } = [];
}

public sealed class ReviewerPoiReviewItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? AdminNote { get; set; }
    public string? CreatorName { get; set; }
    public string? OwnerName { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int TranslationCount { get; set; }
    public int MissingLanguageCount { get; set; }
    public int MissingAudioCount { get; set; }
    public int QualityScore { get; set; }
    public IReadOnlyList<string> Issues { get; set; } = [];
}

public sealed class ReviewerPoiDetailsViewModel
{
    public Poi Poi { get; set; } = new();
    public string Name { get; set; } = "";
    public int QualityScore { get; set; }
    public IReadOnlyList<string> ActiveLanguages { get; set; } = [];
    public IReadOnlyList<string> MissingLanguages { get; set; } = [];
    public IReadOnlyList<string> Issues { get; set; } = [];
    public IReadOnlyList<PoiReview> RecentReviews { get; set; } = [];
}
