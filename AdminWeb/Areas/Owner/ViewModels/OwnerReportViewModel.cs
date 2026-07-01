using AdminWeb.Models;

namespace AdminWeb.Areas.Owner.ViewModels;

public sealed class OwnerReportViewModel
{
    public OwnerProfile Owner { get; set; } = new();
    public int PoiCount { get; set; }
    public int MenuItemCount { get; set; }
    public int TotalPlayCount { get; set; }
    public int QrScanCount { get; set; }
    public int TotalReviewCount { get; set; }
    public double AverageRating { get; set; }
    public string RangePreset { get; set; } = "1m";
    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }
    public DateTime? CustomStart { get; set; }
    public DateTime? CustomEnd { get; set; }
    public string RangeLabel { get; set; } = "1 tháng gần nhất";

    // Subscription/payments to the platform.
    public decimal PaidRevenue { get; set; }
    public int PendingPayments { get; set; }

    // Real shop/menu revenue from tourist orders.
    public decimal PlatformCommissionRate { get; set; } = 0.10m;
    public decimal RangeOrderRevenue { get; set; }
    public decimal TodayOrderRevenue { get; set; }
    public decimal Last30DaysOrderRevenue { get; set; }
    public decimal CompletedOrderRevenue { get; set; }
    public decimal PaidOrderRevenue { get; set; }
    public decimal PlatformCommissionAmount { get; set; }
    public decimal OwnerNetRevenue { get; set; }
    public decimal PendingOrderAmount { get; set; }
    public int TotalOrderCount { get; set; }
    public int CompletedOrderCount { get; set; }
    public int PendingOrderCount { get; set; }
    public int CancelledOrderCount { get; set; }

    public List<OwnerReportPoiRow> TopPois { get; set; } = [];
    public List<OwnerReportDayRow> Last14Days { get; set; } = [];
    public List<OwnerRevenueDayRow> RevenueLast14Days { get; set; } = [];
    public List<OwnerTopMenuItemRow> TopMenuItems { get; set; } = [];
    public List<MenuOrder> RecentOrders { get; set; } = [];
    public List<PoiReview> RecentReviews { get; set; } = [];
}

public sealed class OwnerReportPoiRow
{
    public int PoiId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public int PlayCount { get; set; }
    public int ReviewCount { get; set; }
    public double AverageRating { get; set; }
}

public sealed class OwnerReportDayRow
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = "";
    public int PlayCount { get; set; }
    public int QrCount { get; set; }
}

public sealed class OwnerRevenueDayRow
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public int OrderCount { get; set; }
}

public sealed class OwnerTopMenuItemRow
{
    public string ItemName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
}
