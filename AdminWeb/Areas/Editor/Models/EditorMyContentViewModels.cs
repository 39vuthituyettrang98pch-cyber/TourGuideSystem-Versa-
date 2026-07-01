namespace AdminWeb.Areas.Editor.Models;

public sealed class EditorMyContentViewModel
{
    public string? Search { get; set; }
    public string StatusFilter { get; set; } = "All";
    public int TotalPoiCount { get; set; }
    public int PendingPoiCount { get; set; }
    public int ApprovedPoiCount { get; set; }
    public int RejectedPoiCount { get; set; }
    public int DraftPoiCount { get; set; }
    public int TourCount { get; set; }
    public IReadOnlyList<EditorMyPoiItemViewModel> Pois { get; set; } = [];
    public IReadOnlyList<EditorMyTourItemViewModel> Tours { get; set; } = [];
}

public sealed class EditorMyPoiItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public int TranslationCount { get; set; }
    public int AudioCount { get; set; }
    public int MenuItemCount { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public sealed class EditorMyTourItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime? LoggedAt { get; set; }
    public int EstimatedTime { get; set; }
    public int PoiCount { get; set; }
}
