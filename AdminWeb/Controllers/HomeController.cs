using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using AdminWeb.Models;
using AdminWeb.ViewModels;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var dashboard = await BuildAdminDashboardAsync();

        // Thống kê tổng quan
        ViewBag.TotalPois = await _context.Pois.CountAsync();
        ViewBag.TotalTours = await _context.Tours.CountAsync();
        ViewBag.TotalListens = await _context.VisitorPlaybackLogs.CountAsync();
        ViewBag.TotalAudioFiles = await _context.PoiTranslations.CountAsync(pt => !string.IsNullOrEmpty(pt.AudioUrl));

        // Dữ liệu cho biểu đồ Lượt nghe 7 ngày qua
        var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);
        var dailyLogs = await _context.VisitorPlaybackLogs
            .Where(l => l.CreatedAt >= sevenDaysAgo)
            .GroupBy(l => l.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var chartLabels = new List<string>();
        var chartData = new List<int>();
        for (int i = 0; i < 7; i++)
        {
            var date = sevenDaysAgo.AddDays(i);
            chartLabels.Add(date.ToString("dd/MM"));
            var match = dailyLogs.FirstOrDefault(d => d.Date == date);
            chartData.Add(match?.Count ?? 0);
        }
        ViewBag.ChartLabels = chartLabels;
        ViewBag.ChartData = chartData;

        // Dữ liệu cho biểu đồ tròn (Tỷ lệ ngôn ngữ)
        var languageStats = await _context.VisitorPlaybackLogs
            .GroupBy(l => l.LanguageCode)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToListAsync();
        ViewBag.PieLabels = languageStats.Select(s => (s.Language ?? "unknown").ToUpper()).ToList();
        ViewBag.PieData = languageStats.Select(s => s.Count).ToList();

        // Top 5 POI
        var topPoisData = await _context.VisitorPlaybackLogs
            .GroupBy(l => l.PoiId)
            .Select(g => new { PoiId = g.Key, ListenCount = g.Count() })
            .OrderByDescending(x => x.ListenCount)
            .Take(5)
            .ToListAsync();

        var topPoiIds = topPoisData.Select(i => i.PoiId).ToList();
        var poisForTop = await _context.Pois
            .AsNoTracking()
            .Include(p => p.Translations)
            .Where(p => topPoiIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var topPoisDict = topPoisData.ToDictionary(
            item => poisForTop.TryGetValue(item.PoiId, out var poi)
                ? poi.Translations?.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? $"POI #{item.PoiId}"
                : $"POI #{item.PoiId}",
            item => item.ListenCount);
        ViewBag.TopPois = topPoisDict;

        // Hoạt động gần đây
        var recentActivities = await _context.VisitorPlaybackLogs.Include(l => l.Poi).ThenInclude(p => p!.Translations).OrderByDescending(l => l.CreatedAt).Take(5).ToListAsync();
        foreach (var log in recentActivities) log.PoiName = log.Poi?.Translations?.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? $"POI #{log.PoiId}";
        ViewBag.RecentActivities = recentActivities;

        // Bản đồ POI và heatmap hoạt động du khách
        var mapPois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Where(poi =>
                poi.Latitude >= -90 && poi.Latitude <= 90 &&
                poi.Longitude >= -180 && poi.Longitude <= 180)
            .OrderBy(poi => poi.Id)
            .ToListAsync();
        ViewBag.PoiMapData = mapPois.Select(poi => new PoiMapItemViewModel
        {
            Id = poi.Id,
            Name = poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")?.Name
                ?? poi.Translations.FirstOrDefault()?.Name
                ?? $"POI #{poi.Id}",
            Latitude = (double)poi.Latitude,
            Longitude = (double)poi.Longitude,
            Radius = poi.Radius,
            Status = poi.Status
        }).ToList();

        ViewBag.HeatmapData = await _context.VisitorPlaybackLogs.Where(l => l.VisitorLatitude != null && l.VisitorLongitude != null).Select(l => new { lat = l.VisitorLatitude, lng = l.VisitorLongitude, intensity = 1 }).ToListAsync();

        return View(dashboard);
    }

    private async Task<AdminDashboardViewModel> BuildAdminDashboardAsync()
    {
        var now = ToReportLocal(DateTime.UtcNow);
        var today = now.Date;
        var activeSince = now.AddMinutes(-30);
        var todayStartUtc = ToReportUtc(today);
        var activeSinceUtc = ToReportUtc(activeSince);

        var todayLogs = await _context.VisitorPlaybackLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= todayStartUtc)
            .ToListAsync();

        var todayDiscoveries = await _context.TouristPoiDiscoveries
            .AsNoTracking()
            .Where(item => item.DiscoveredAt >= todayStartUtc)
            .ToListAsync();

        var todayOrders = await _context.MenuOrders
            .AsNoTracking()
            .Where(order => order.CreatedAt >= todayStartUtc)
            .ToListAsync();

        var activeLogs = todayLogs.Where(log => log.CreatedAt >= activeSinceUtc).ToList();
        var activeOrders = todayOrders
            .Where(order => order.CreatedAt >= activeSinceUtc && !IsClosedOrder(order.Status))
            .ToList();

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Include(poi => poi.OwnerProfile)
            .Where(poi => poi.Status == "Approved")
            .ToListAsync();

        var menuPoiIds = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => item.Status != "Hidden")
            .Select(item => item.PoiId)
            .Distinct()
            .ToListAsync();

        var menuPoiSet = menuPoiIds.ToHashSet();
        var poiRows = pois
            .Select(poi => new AdminLocationVisitorViewModel
            {
                PoiId = poi.Id,
                PoiName = GetPoiName(poi),
                OwnerName = poi.OwnerProfile?.BusinessName ?? "",
                HasMenu = menuPoiSet.Contains(poi.Id) || todayOrders.Any(order => order.PoiId == poi.Id),
                EstimatedActiveVisitors = CountDistinctVisitors(
                    activeLogs
                        .Where(log => log.PoiId == poi.Id)
                        .Select(GetVisitorKey)
                        .Concat(activeOrders
                            .Where(order => order.PoiId == poi.Id)
                            .Select(order => TouristKey(order.TouristId)))),
                UniqueVisitorsToday = CountDistinctVisitors(
                    todayLogs
                        .Where(log => log.PoiId == poi.Id)
                        .Select(GetVisitorKey)
                        .Concat(todayDiscoveries
                            .Where(item => item.PoiId == poi.Id)
                            .Select(item => TouristKey(item.TouristId)))
                        .Concat(todayOrders
                            .Where(order => order.PoiId == poi.Id)
                            .Select(order => TouristKey(order.TouristId)))),
                CheckInsToday = todayDiscoveries.Count(item => item.PoiId == poi.Id),
                InteractionsToday = todayLogs.Count(log => log.PoiId == poi.Id),
                MenuOrdersToday = todayOrders.Count(order => order.PoiId == poi.Id),
                PendingOrders = todayOrders.Count(order => order.PoiId == poi.Id && !IsClosedOrder(order.Status)),
                RevenueToday = todayOrders
                    .Where(order => order.PoiId == poi.Id && !string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    .Sum(order => order.TotalAmount),
                LastActivityAt = Latest(
                    todayLogs.Where(log => log.PoiId == poi.Id).Select(log => (DateTime?)ToReportLocal(log.CreatedAt))
                        .Concat(todayDiscoveries.Where(item => item.PoiId == poi.Id).Select(item => (DateTime?)ToReportLocal(item.DiscoveredAt)))
                        .Concat(todayOrders.Where(order => order.PoiId == poi.Id).Select(order => (DateTime?)ToReportLocal(order.CreatedAt))))
            })
            .OrderByDescending(row => row.EstimatedActiveVisitors)
            .ThenByDescending(row => row.UniqueVisitorsToday)
            .ThenByDescending(row => row.MenuOrdersToday)
            .ThenByDescending(row => row.InteractionsToday)
            .ThenBy(row => row.PoiName)
            .Take(10)
            .ToList();

        var hourlyTraffic = Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var hourLogs = todayLogs.Where(log => ToReportLocal(log.CreatedAt).Hour == hour).ToList();
                var hourDiscoveries = todayDiscoveries.Where(item => ToReportLocal(item.DiscoveredAt).Hour == hour).ToList();
                var hourOrders = todayOrders.Where(order => ToReportLocal(order.CreatedAt).Hour == hour).ToList();

                return new AdminHourlyTrafficViewModel
                {
                    Hour = hour,
                    Label = $"{hour:00}:00",
                    CheckIns = hourDiscoveries.Count,
                    Interactions = hourLogs.Count,
                    MenuOrders = hourOrders.Count,
                    UniqueVisitors = CountDistinctVisitors(
                        hourLogs.Select(GetVisitorKey)
                            .Concat(hourDiscoveries.Select(item => TouristKey(item.TouristId)))
                            .Concat(hourOrders.Select(order => TouristKey(order.TouristId))))
                };
            })
            .ToList();

        var peakHour = hourlyTraffic
            .OrderByDescending(item => item.UniqueVisitors)
            .ThenByDescending(item => item.CheckIns + item.Interactions + item.MenuOrders)
            .FirstOrDefault();

        var busiestLocation = poiRows
            .OrderByDescending(row => row.EstimatedActiveVisitors)
            .ThenByDescending(row => row.UniqueVisitorsToday)
            .FirstOrDefault();

        var activeVisitorKeys = activeLogs.Select(GetVisitorKey)
            .Concat(activeOrders.Select(order => TouristKey(order.TouristId)));

        var activeTodayKeys = todayLogs.Select(GetVisitorKey)
            .Concat(todayDiscoveries.Select(item => TouristKey(item.TouristId)))
            .Concat(todayOrders.Select(order => TouristKey(order.TouristId)));
 
        var closedStatuses = new[] { "Completed", "Cancelled" };
        var pendingMenuOrders = await _context.MenuOrders
            .AsNoTracking()
            .CountAsync(order => !closedStatuses.Contains(order.Status));

        var dashboard = new AdminDashboardViewModel
        {
            TotalTourists = await _context.Tourists.CountAsync(),
            ActiveTouristsToday = CountDistinctVisitors(activeTodayKeys),
            EstimatedActiveVisitors = CountDistinctVisitors(activeVisitorKeys),
            CheckInsToday = todayDiscoveries.Count,
            MenuOrdersToday = todayOrders.Count,
            PendingMenuOrders = pendingMenuOrders,
            MenuRevenueToday = todayOrders
                .Where(order => !string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .Sum(order => order.TotalAmount),
            PeakHourLabel = peakHour != null && peakHour.UniqueVisitors > 0
                ? $"{peakHour.Hour:00}:00 - {peakHour.Hour:00}:59"
                : "Chưa có dữ liệu",
            BusiestLocationName = busiestLocation?.PoiName ?? "Chưa có dữ liệu",
            BusiestLocationVisitors = busiestLocation?.EstimatedActiveVisitors ?? 0,
            HourlyTraffic = hourlyTraffic,
            LocationVisitors = poiRows
        };

        dashboard.Alerts = BuildDashboardAlerts(dashboard, pendingMenuOrders);

        return dashboard;
    }

    private static List<AdminDashboardAlertViewModel> BuildDashboardAlerts(
        AdminDashboardViewModel dashboard,
        int pendingMenuOrders)
    {
        var alerts = new List<AdminDashboardAlertViewModel>();

        if (dashboard.EstimatedActiveVisitors > 0)
        {
            alerts.Add(new AdminDashboardAlertViewModel
            {
                Icon = "fa-person-walking",
                Title = "Đang có khách hoạt động",
                Description = $"{dashboard.EstimatedActiveVisitors} khách có tương tác trong 30 phút gần nhất. Địa điểm sôi động nhất: {dashboard.BusiestLocationName}.",
                Tone = "success",
                Url = "/Admin/ActivityLog"
            });
        }
        else
        {
            alerts.Add(new AdminDashboardAlertViewModel
            {
                Icon = "fa-clock",
                Title = "Chưa có khách mới trong 30 phút",
                Description = "Theo dõi heatmap và bảng theo địa điểm để nhận diện khung giờ vàng trong ngày.",
                Tone = "info",
                Url = "/Admin/ActivityLog"
            });
        }

        if (pendingMenuOrders > 0)
        {
            alerts.Add(new AdminDashboardAlertViewModel
            {
                Icon = "fa-bell-concierge",
                Title = "Đơn menu cần xử lý",
                Description = $"{pendingMenuOrders} đơn menu chưa đóng trạng thái. Nên liên hệ chủ quán nếu đơn bị trễ.",
                Tone = "warning"
            });
        }

        if (dashboard.CheckInsToday == 0)
        {
            alerts.Add(new AdminDashboardAlertViewModel
            {
                Icon = "fa-location-crosshairs",
                Title = "Hôm nay chưa có check-in GPS",
                Description = "Kiểm tra QR, hướng dẫn check-in và bán kính POI nếu du khách báo không check-in được.",
                Tone = "warning",
                Url = "/Admin/Poi"
            });
        }

        return alerts.Take(4).ToList();
    }

    private static string GetPoiName(Poi poi)
    {
        return poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")?.Name
            ?? poi.Translations.FirstOrDefault()?.Name
            ?? $"POI #{poi.Id}";
    }

    private static int CountDistinctVisitors(IEnumerable<string> visitorKeys)
    {
        return visitorKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static string GetVisitorKey(VisitorPlaybackLog log)
    {
        return log.TouristId.HasValue
            ? TouristKey(log.TouristId.Value)
            : string.IsNullOrWhiteSpace(log.DeviceId)
                ? $"log:{log.Id}"
                : $"device:{log.DeviceId.Trim()}";
    }

    private static string TouristKey(int touristId) => $"tourist:{touristId}";

    private static bool IsClosedOrder(string? status)
    {
        return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? Latest(IEnumerable<DateTime?> values)
    {
        return values
            .Where(value => value.HasValue)
            .DefaultIfEmpty()
            .Max();
    }


    private static DateTime ToReportLocal(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, ReportTimeZone);
    }

    private static DateTime ToReportUtc(DateTime localValue)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified), ReportTimeZone);
    }

    private static TimeZoneInfo ReportTimeZone { get; } = ResolveReportTimeZone();

    private static TimeZoneInfo ResolveReportTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    [AllowAnonymous]
    public IActionResult HttpStatus(int code)
    {
        Response.StatusCode = code;
        ViewBag.StatusCode = code;
        return View("StatusCode");
    }
}
