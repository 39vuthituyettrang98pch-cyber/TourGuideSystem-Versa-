using AdminWeb.Models;

namespace AdminWeb.Areas.Editor.Models;

public sealed class EditorContentBoardViewModel
{
    public string StatusFilter { get; set; } = "All";
    public string? Search { get; set; }
    public string IssueFilter { get; set; } = "All";
    public int TotalCount { get; set; }
    public int DraftCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int NeedsWorkCount { get; set; }
    public IReadOnlyList<string> ActiveLanguages { get; set; } = [];
    public IReadOnlyList<EditorPoiCardViewModel> Items { get; set; } = [];
}

public sealed class EditorPoiCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? AdminNote { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int Radius { get; set; }
    public int TranslationCount { get; set; }
    public int RequiredLanguageCount { get; set; }
    public int MissingLanguageCount { get; set; }
    public int MissingAudioCount { get; set; }
    public int MissingScriptCount { get; set; }
    public int MediaCount { get; set; }
    public int CategoryCount { get; set; }
    public int QualityScore { get; set; }
    public IReadOnlyList<string> MissingLanguages { get; set; } = [];
    public IReadOnlyList<string> Issues { get; set; } = [];
}

public sealed class EditorQualityReportViewModel
{
    public int TotalPois { get; set; }
    public int GoodPois { get; set; }
    public int WarningPois { get; set; }
    public int CriticalPois { get; set; }
    public IReadOnlyList<string> ActiveLanguages { get; set; } = [];
    public IReadOnlyList<EditorPoiCardViewModel> Items { get; set; } = [];
}
