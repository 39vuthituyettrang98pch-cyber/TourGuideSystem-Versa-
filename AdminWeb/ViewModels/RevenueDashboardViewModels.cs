using AdminWeb.Models;

namespace AdminWeb.ViewModels;

public sealed class AdminRevenueDashboardViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string RangePreset { get; set; } = "1m";
    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }
    public DateTime? CustomStart { get; set; }
    public DateTime? CustomEnd { get; set; }
    public string RangeLabel { get; set; } = "1 tháng gần nhất";
    public decimal PlatformCommissionRate { get; set; } = 0.10m;
    public decimal PlatformSubscriptionRevenue { get; set; }
    public decimal OwnerSubscriptionRevenue { get; set; }
    public decimal TouristSubscriptionRevenue { get; set; }
    public decimal PlatformCommissionRevenue { get; set; }
    public decimal PlatformRevenueTotal { get; set; }
    public decimal GrossMenuSales { get; set; }
    public decimal CompletedMenuSales { get; set; }
    public decimal PaidMenuSales { get; set; }
    public decimal PendingMenuSales { get; set; }
    public decimal OwnerNetMenuRevenue { get; set; }
    public int PaidPaymentCount { get; set; }
    public int PendingPaymentCount { get; set; }
    public int CompletedOrderCount { get; set; }
    public int PendingOrderCount { get; set; }
    public int CancelledOrderCount { get; set; }
    public List<AdminRevenueOwnerRow> TopOwners { get; set; } = [];
    public List<AdminRevenueDayRow> Last14Days { get; set; } = [];
    public List<PaymentTransaction> RecentPayments { get; set; } = [];
    public List<MenuOrder> RecentOrders { get; set; } = [];
}

public sealed class AdminRevenueOwnerRow
{
    public int OwnerProfileId { get; set; }
    public string BusinessName { get; set; } = "";
    public int CompletedOrders { get; set; }
    public decimal GrossSales { get; set; }
    public decimal PaidSales { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal OwnerNetSales { get; set; }
    public decimal SubscriptionPaid { get; set; }
}

public sealed class AdminRevenueDayRow
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = "";
    public decimal PlatformRevenue { get; set; }
    public decimal SubscriptionRevenue { get; set; }
    public decimal CommissionRevenue { get; set; }
    public decimal MenuSales { get; set; }
    public decimal OwnerNetSales { get; set; }
    public int Orders { get; set; }
}
