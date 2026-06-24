namespace UserMobile.Models;

public sealed class TouristPremiumStatus
{
    public bool IsPremium { get; set; }
    public string PlanName { get; set; } = "Gói miễn phí";
    public DateTime? ExpiresAt { get; set; }
    public bool VnPayAvailable { get; set; }
    public bool MomoAvailable { get; set; }
    public bool MomoDemoMode { get; set; }

    public string StatusTitle => IsPremium ? "Premium đang hoạt động" : "Mở khóa toàn bộ hành trình";
    public string StatusDescription => IsPremium
        ? $"{PlanName} · đến {ExpiresAt?.ToLocalTime():dd/MM/yyyy}"
        : "Tour gợi ý, AI hướng dẫn viên và trải nghiệm thuyết minh nâng cao.";
}

public sealed class AudioPlaybackAccessResult
{
    public bool IsPremium { get; set; }
    public int DailyLimit { get; set; }
    public int UsedToday { get; set; }
    public int? RemainingToday { get; set; }
}

public sealed class TouristPaymentPlan
{
    public int Id { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PriceText => Price <= 0 ? "Miễn phí" : $"{Price:N0} đ";
    public string DurationText => DurationDays > 0 ? $"{DurationDays} ngày" : "Không giới hạn";
}

public sealed class TouristPaymentHistory
{
    public int Id { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string AmountText => $"{Amount:N0} {Currency}";
    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

public sealed class TouristCheckoutResult
{
    public int PaymentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CheckoutUrl { get; set; }
}

public sealed class LeaderboardItem
{
    public int Rank { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int DiscoveredPoiCount { get; set; }
    public string RankName { get; set; } = string.Empty;
    public bool IsCurrentUser { get; set; }
    public string RankText => Rank <= 3 ? new[] { "🥇", "🥈", "🥉" }[Rank - 1] : $"#{Rank}";
    public string DiscoveryText => $"{DiscoveredPoiCount} POI";
    public string CardColor => IsCurrentUser ? "#F0EDFF" : "#FFFFFF";
}
