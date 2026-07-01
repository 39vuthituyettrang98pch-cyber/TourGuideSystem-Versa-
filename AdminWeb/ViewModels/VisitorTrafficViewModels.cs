namespace AdminWeb.ViewModels;

public sealed class VisitorTrafficReportViewModel
{
    public int? SelectedPoiId { get; set; }
    public string SelectedPoiName { get; set; } = "Tất cả địa điểm / quán";
    public DateTime Date { get; set; } = DateTime.Today;
    public string StartTime { get; set; } = "00:00";
    public string EndTime { get; set; } = "23:59";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public IReadOnlyList<VisitorTrafficPoiOptionViewModel> PoiOptions { get; set; } = [];

    public int UniqueVisitors { get; set; }
    public int TotalEvents { get; set; }
    public int AudioInteractions { get; set; }
    public int GpsCheckIns { get; set; }
    public int MenuOrders { get; set; }
    public int PendingMenuOrders { get; set; }
    public decimal MenuRevenue { get; set; }
    public string PeakHourLabel { get; set; } = "Chưa có dữ liệu";
    public int PeakHourVisitors { get; set; }

    public IReadOnlyList<VisitorTrafficHourlyRowViewModel> HourlyRows { get; set; } = [];
    public IReadOnlyList<VisitorTrafficPoiRowViewModel> PoiRows { get; set; } = [];
    public IReadOnlyList<VisitorTrafficEventRowViewModel> RecentEvents { get; set; } = [];
}

public sealed class VisitorTrafficPoiOptionViewModel
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public bool HasMenu { get; set; }
}

public sealed class VisitorTrafficHourlyRowViewModel
{
    public int Hour { get; set; }
    public string Label { get; set; } = "";
    public int UniqueVisitors { get; set; }
    public int AudioInteractions { get; set; }
    public int GpsCheckIns { get; set; }
    public int MenuOrders { get; set; }
    public decimal MenuRevenue { get; set; }
    public int TotalEvents => AudioInteractions + GpsCheckIns + MenuOrders;
}

public sealed class VisitorTrafficPoiRowViewModel
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public bool HasMenu { get; set; }
    public int UniqueVisitors { get; set; }
    public int AudioInteractions { get; set; }
    public int GpsCheckIns { get; set; }
    public int MenuOrders { get; set; }
    public int PendingMenuOrders { get; set; }
    public decimal MenuRevenue { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public int TotalEvents => AudioInteractions + GpsCheckIns + MenuOrders;
}

public sealed class VisitorTrafficEventRowViewModel
{
    public DateTime CreatedAt { get; set; }
    public string EventType { get; set; } = "";
    public string PoiName { get; set; } = "";
    public string VisitorLabel { get; set; } = "";
    public string Detail { get; set; } = "";
}
