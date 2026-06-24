using AdminWeb.Models;

namespace AdminWeb.ViewModels;

public sealed class AdminPurchaseDashboardViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string RangePreset { get; set; } = "1m";
    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }
    public DateTime? CustomStart { get; set; }
    public DateTime? CustomEnd { get; set; }
    public string RangeLabel { get; set; } = "1 tháng gần nhất";

    public int TotalPurchases { get; set; }
    public int PaidPurchases { get; set; }
    public int PendingPurchases { get; set; }
    public int CancelledPurchases { get; set; }
    public int FailedPurchases { get; set; }

    public int OwnerPurchaseCount { get; set; }
    public int TouristPurchaseCount { get; set; }
    public int OwnerPaidCount { get; set; }
    public int TouristPaidCount { get; set; }

    public decimal OwnerRevenue { get; set; }
    public decimal TouristRevenue { get; set; }
    public decimal TotalRevenue { get; set; }

    public int ActiveOwnerSubscriptions { get; set; }
    public int ActiveTouristSubscriptions { get; set; }

    public List<AdminPurchasePlanRow> OwnerPlanRows { get; set; } = [];
    public List<AdminPurchasePlanRow> TouristPlanRows { get; set; } = [];
    public List<AdminPurchaseBuyerRow> TopOwnerBuyers { get; set; } = [];
    public List<AdminPurchaseBuyerRow> TopTouristBuyers { get; set; } = [];
    public List<AdminPurchaseDayRow> Last14Days { get; set; } = [];
    public List<PaymentTransaction> RecentTransactions { get; set; } = [];
}

public sealed class AdminPurchasePlanRow
{
    public string PlanCode { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string Audience { get; set; } = "";
    public int PaidCount { get; set; }
    public int PendingCount { get; set; }
    public int CancelledCount { get; set; }
    public int ActiveSubscriptions { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class AdminPurchaseBuyerRow
{
    public int? OwnerProfileId { get; set; }
    public int? TouristId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Contact { get; set; } = "";
    public int PaidCount { get; set; }
    public decimal TotalPaid { get; set; }
    public DateTime? LastPaidAt { get; set; }
}

public sealed class AdminPurchaseDayRow
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = "";
    public int OwnerPurchases { get; set; }
    public int TouristPurchases { get; set; }
    public decimal OwnerRevenue { get; set; }
    public decimal TouristRevenue { get; set; }
}
