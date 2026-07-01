namespace AdminWeb.ViewModels;

public sealed class AdminDashboardViewModel
{
    public int TotalTourists { get; set; }
    public int ActiveTouristsToday { get; set; }
    public int EstimatedActiveVisitors { get; set; }
    public int CheckInsToday { get; set; }
    public int MenuOrdersToday { get; set; }
    public int PendingMenuOrders { get; set; }
    public decimal MenuRevenueToday { get; set; }
    public string PeakHourLabel { get; set; } = "Chưa có dữ liệu";
    public string BusiestLocationName { get; set; } = "Chưa có dữ liệu";
    public int BusiestLocationVisitors { get; set; }
    public IReadOnlyList<AdminHourlyTrafficViewModel> HourlyTraffic { get; set; } = [];
    public IReadOnlyList<AdminLocationVisitorViewModel> LocationVisitors { get; set; } = [];
    public IReadOnlyList<AdminDashboardAlertViewModel> Alerts { get; set; } = [];
}

public sealed class AdminHourlyTrafficViewModel
{
    public int Hour { get; set; }
    public string Label { get; set; } = "";
    public int CheckIns { get; set; }
    public int Interactions { get; set; }
    public int MenuOrders { get; set; }
    public int UniqueVisitors { get; set; }
}

public sealed class AdminLocationVisitorViewModel
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public bool HasMenu { get; set; }
    public int EstimatedActiveVisitors { get; set; }
    public int UniqueVisitorsToday { get; set; }
    public int CheckInsToday { get; set; }
    public int InteractionsToday { get; set; }
    public int MenuOrdersToday { get; set; }
    public int PendingOrders { get; set; }
    public decimal RevenueToday { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public sealed class AdminDashboardAlertViewModel
{
    public string Icon { get; set; } = "fa-circle-info";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tone { get; set; } = "info";
    public string? Url { get; set; }
}
