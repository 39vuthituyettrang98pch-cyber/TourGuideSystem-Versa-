using AdminWeb.Models;

namespace AdminWeb.ViewModels;

public sealed class VisitorListItemViewModel
{
    public Tourist Tourist { get; init; } = new();
    public int TotalPoints { get; init; }
    public int DiscoveredPoiCount { get; init; }
    public string RankName { get; init; } = "";
}

public sealed class VisitorAchievementDetailsViewModel
{
    public Tourist Tourist { get; init; } = new();
    public int TotalPoints { get; init; }
    public int DiscoveredPoiCount { get; init; }
    public int TotalPoiCount { get; init; }
    public double CompletionPercentage { get; init; }
    public string RankName { get; init; } = "";
    public string NextRankName { get; init; } = "";
    public int PointsToNextRank { get; init; }
    public List<VisitorAchievementBadgeViewModel> Achievements { get; init; } = [];
    public List<VisitorDiscoveryViewModel> Discoveries { get; init; } = [];
}

public sealed class VisitorAchievementBadgeViewModel
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "";
    public int CurrentValue { get; init; }
    public int RequiredValue { get; init; }
    public bool IsUnlocked { get; init; }
}

public sealed class VisitorDiscoveryViewModel
{
    public int PoiId { get; init; }
    public string PoiName { get; init; } = "";
    public string Method { get; init; } = "";
    public int Points { get; init; }
    public DateTime DiscoveredAt { get; init; }
}

public sealed class PoiQrViewModel
{
    public int PoiId { get; init; }
    public string PoiName { get; init; } = "";
    public string QrCodeToken { get; init; } = "";
    public string QrPayload { get; init; } = "";
}
