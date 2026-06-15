namespace UserMobile.Models;

public sealed class AchievementSummary
{
    public int TotalPoints { get; set; }
    public int DiscoveredPoiCount { get; set; }
    public int TotalPoiCount { get; set; }
    public double CompletionPercentage { get; set; }
    public string RankName { get; set; } = string.Empty;
    public string NextRankName { get; set; } = string.Empty;
    public int PointsToNextRank { get; set; }
    public double RankProgress { get; set; }
    public List<AchievementItem> Achievements { get; set; } = [];
    public List<RecentDiscovery> RecentDiscoveries { get; set; } = [];

    public string DiscoveryProgressText => $"{DiscoveredPoiCount}/{TotalPoiCount} POI đã khám phá";
    public string NextRankText => PointsToNextRank > 0
        ? $"Còn {PointsToNextRank} điểm để đạt {NextRankName}"
        : "Bạn đã đạt hạng cao nhất";
    public bool HasRecentDiscoveries => RecentDiscoveries.Count > 0;
    public bool HasNoRecentDiscoveries => !HasRecentDiscoveries;
}

public sealed class AchievementItem
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int RequiredValue { get; set; }
    public int CurrentValue { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }

    public double Progress => RequiredValue == 0
        ? 0
        : Math.Clamp((double)CurrentValue / RequiredValue, 0, 1);
    public string ProgressText => IsUnlocked ? "Đã mở khóa" : $"{CurrentValue}/{RequiredValue}";
    public string StateColor => IsUnlocked ? "#5B3FE4" : "#8B95A7";
    public string CardColor => IsUnlocked ? "#F0EDFF" : "#F5F6F8";
    public double CardOpacity => IsUnlocked ? 1 : 0.68;
}

public sealed class RecentDiscovery
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int Points { get; set; }
    public DateTime DiscoveredAt { get; set; }

    public string MethodText => Method.Equals("QR", StringComparison.OrdinalIgnoreCase)
        ? "Quét QR"
        : "Check-in GPS";
    public string PointsText => $"+{Points} điểm";
    public string DiscoveredAtText => DiscoveredAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

public sealed class DiscoveryResult
{
    public bool IsNewDiscovery { get; set; }
    public int PointsAwarded { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<AchievementItem> NewlyUnlockedAchievements { get; set; } = [];
    public AchievementSummary Summary { get; set; } = new();

    public string BuildDisplayMessage()
    {
        if (!IsNewDiscovery)
            return Message;

        var achievementText = NewlyUnlockedAchievements.Count == 0
            ? string.Empty
            : $"\nMở khóa: {string.Join(", ", NewlyUnlockedAchievements.Select(item => item.Title))}";
        return $"{Message}{achievementText}";
    }
}
