using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class VisitorTrafficController : Controller
{
    private readonly AppDbContext _context;

    public VisitorTrafficController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(
        int? poiId,
        DateTime? date,
        string? startTime,
        string? endTime,
        CancellationToken cancellationToken)
    {
        var todayLocal = ToReportLocal(DateTime.UtcNow).Date;
        var selectedDate = (date ?? todayLocal).Date;
        var start = ParseClock(startTime, new TimeSpan(0, 0, 0));
        var end = ParseClock(endTime, new TimeSpan(23, 59, 59));

        var startAtLocal = selectedDate.Add(start);
        var endAtLocal = selectedDate.Add(end);

        if (endAtLocal <= startAtLocal)
            endAtLocal = startAtLocal.AddHours(1);

        // Tạo dữ liệu demo theo ngày hiện tại để trang báo cáo không bị trắng khi demo trên Cloudflare/local.
        // Dữ liệu có prefix riêng nên không ảnh hưởng dữ liệu thật của người dùng.
        if (selectedDate == todayLocal)
            await EnsureDemoTrafficForDayAsync(selectedDate, cancellationToken);

        var queryStartUtc = ToReportUtc(startAtLocal);
        var queryEndUtc = ToReportUtc(endAtLocal);

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Include(poi => poi.OwnerProfile)
            .OrderBy(poi => poi.Id)
            .ToListAsync(cancellationToken);

        var menuPoiIds = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => item.Status != "Hidden")
            .Select(item => item.PoiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var menuPoiSet = menuPoiIds.ToHashSet();
        var poiOptions = pois.Select(poi => new VisitorTrafficPoiOptionViewModel
            {
                PoiId = poi.Id,
                PoiName = GetPoiName(poi),
                OwnerName = poi.OwnerProfile?.BusinessName ?? "",
                HasMenu = menuPoiSet.Contains(poi.Id)
            })
            .OrderBy(item => item.PoiName)
            .ToList();

        var validPoiIds = pois.Select(poi => poi.Id).ToHashSet();
        if (poiId.HasValue && !validPoiIds.Contains(poiId.Value))
            poiId = null;

        var selectedPoiIds = poiId.HasValue
            ? new List<int> { poiId.Value }
            : validPoiIds.ToList();

        var selectedPoiName = poiId.HasValue
            ? poiOptions.FirstOrDefault(item => item.PoiId == poiId.Value)?.PoiName ?? $"POI #{poiId.Value}"
            : "Tất cả địa điểm / quán";

        var logs = await _context.VisitorPlaybackLogs
            .AsNoTracking()
            .Where(log => selectedPoiIds.Contains(log.PoiId)
                && log.CreatedAt >= queryStartUtc
                && log.CreatedAt <= queryEndUtc)
            .ToListAsync(cancellationToken);

        var discoveries = await _context.TouristPoiDiscoveries
            .AsNoTracking()
            .Where(item => selectedPoiIds.Contains(item.PoiId)
                && item.DiscoveredAt >= queryStartUtc
                && item.DiscoveredAt <= queryEndUtc)
            .ToListAsync(cancellationToken);

        var orders = await _context.MenuOrders
            .AsNoTracking()
            .Where(order => selectedPoiIds.Contains(order.PoiId)
                && order.CreatedAt >= queryStartUtc
                && order.CreatedAt <= queryEndUtc)
            .ToListAsync(cancellationToken);

        var poiNameById = poiOptions.ToDictionary(item => item.PoiId, item => item.PoiName);
        var ownerNameById = poiOptions.ToDictionary(item => item.PoiId, item => item.OwnerName);
        var hasMenuById = poiOptions.ToDictionary(item => item.PoiId, item => item.HasMenu);

        var logsWithLocalTime = logs
            .Select(log => new { Log = log, LocalTime = ToReportLocal(log.CreatedAt) })
            .ToList();
        var discoveriesWithLocalTime = discoveries
            .Select(item => new { Discovery = item, LocalTime = ToReportLocal(item.DiscoveredAt) })
            .ToList();
        var ordersWithLocalTime = orders
            .Select(order => new { Order = order, LocalTime = ToReportLocal(order.CreatedAt) })
            .ToList();

        var hourlyRows = Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var hourStart = selectedDate.AddHours(hour);
                var hourEnd = hourStart.AddHours(1).AddTicks(-1);

                var hourLogs = logsWithLocalTime.Where(item => item.LocalTime >= hourStart && item.LocalTime <= hourEnd).Select(item => item.Log).ToList();
                var hourDiscoveries = discoveriesWithLocalTime.Where(item => item.LocalTime >= hourStart && item.LocalTime <= hourEnd).Select(item => item.Discovery).ToList();
                var hourOrders = ordersWithLocalTime.Where(item => item.LocalTime >= hourStart && item.LocalTime <= hourEnd).Select(item => item.Order).ToList();

                return new VisitorTrafficHourlyRowViewModel
                {
                    Hour = hour,
                    Label = $"{hour:00}:00 - {hour:00}:59",
                    AudioInteractions = hourLogs.Count,
                    GpsCheckIns = hourDiscoveries.Count,
                    MenuOrders = hourOrders.Count,
                    MenuRevenue = hourOrders
                        .Where(order => !IsCancelledOrder(order.Status))
                        .Sum(order => order.TotalAmount),
                    UniqueVisitors = CountDistinctVisitors(
                        hourLogs.Select(GetVisitorKey)
                            .Concat(hourDiscoveries.Select(item => TouristKey(item.TouristId)))
                            .Concat(hourOrders.Select(order => TouristKey(order.TouristId))))
                };
            })
            .Where(row => selectedDate.AddHours(row.Hour) <= endAtLocal
                && selectedDate.AddHours(row.Hour + 1).AddTicks(-1) >= startAtLocal)
            .ToList();

        var poiRows = selectedPoiIds
            .Select(id =>
            {
                var poiLogs = logs.Where(log => log.PoiId == id).ToList();
                var poiDiscoveries = discoveries.Where(item => item.PoiId == id).ToList();
                var poiOrders = orders.Where(order => order.PoiId == id).ToList();

                var lastActivityAtUtc = Latest(
                    poiLogs.Select(log => (DateTime?)log.CreatedAt)
                        .Concat(poiDiscoveries.Select(item => (DateTime?)item.DiscoveredAt))
                        .Concat(poiOrders.Select(order => (DateTime?)order.CreatedAt)));

                return new VisitorTrafficPoiRowViewModel
                {
                    PoiId = id,
                    PoiName = poiNameById.TryGetValue(id, out var name) ? name : $"POI #{id}",
                    OwnerName = ownerNameById.TryGetValue(id, out var ownerName) ? ownerName : "",
                    HasMenu = hasMenuById.TryGetValue(id, out var hasMenu) && hasMenu,
                    AudioInteractions = poiLogs.Count,
                    GpsCheckIns = poiDiscoveries.Count,
                    MenuOrders = poiOrders.Count,
                    PendingMenuOrders = poiOrders.Count(order => !IsClosedOrder(order.Status)),
                    MenuRevenue = poiOrders
                        .Where(order => !IsCancelledOrder(order.Status))
                        .Sum(order => order.TotalAmount),
                    UniqueVisitors = CountDistinctVisitors(
                        poiLogs.Select(GetVisitorKey)
                            .Concat(poiDiscoveries.Select(item => TouristKey(item.TouristId)))
                            .Concat(poiOrders.Select(order => TouristKey(order.TouristId)))),
                    LastActivityAt = lastActivityAtUtc.HasValue ? ToReportLocal(lastActivityAtUtc.Value) : null
                };
            })
            .Where(row => row.TotalEvents > 0 || poiId.HasValue)
            .OrderByDescending(row => row.UniqueVisitors)
            .ThenByDescending(row => row.TotalEvents)
            .ThenBy(row => row.PoiName)
            .ToList();

        var peakHour = hourlyRows
            .OrderByDescending(row => row.UniqueVisitors)
            .ThenByDescending(row => row.TotalEvents)
            .FirstOrDefault();

        var recentEvents = BuildRecentEvents(logs, discoveries, orders, poiNameById);

        var model = new VisitorTrafficReportViewModel
        {
            SelectedPoiId = poiId,
            SelectedPoiName = selectedPoiName,
            Date = selectedDate,
            StartTime = startAtLocal.ToString("HH:mm"),
            EndTime = endAtLocal.ToString("HH:mm"),
            StartAt = startAtLocal,
            EndAt = endAtLocal,
            PoiOptions = poiOptions,
            AudioInteractions = logs.Count,
            GpsCheckIns = discoveries.Count,
            MenuOrders = orders.Count,
            PendingMenuOrders = orders.Count(order => !IsClosedOrder(order.Status)),
            MenuRevenue = orders
                .Where(order => !IsCancelledOrder(order.Status))
                .Sum(order => order.TotalAmount),
            UniqueVisitors = CountDistinctVisitors(
                logs.Select(GetVisitorKey)
                    .Concat(discoveries.Select(item => TouristKey(item.TouristId)))
                    .Concat(orders.Select(order => TouristKey(order.TouristId)))),
            TotalEvents = logs.Count + discoveries.Count + orders.Count,
            PeakHourLabel = peakHour != null && peakHour.TotalEvents > 0 ? peakHour.Label : "Chưa có dữ liệu",
            PeakHourVisitors = peakHour?.UniqueVisitors ?? 0,
            HourlyRows = hourlyRows,
            PoiRows = poiRows,
            RecentEvents = recentEvents
        };

        return View(model);
    }

    private async Task EnsureDemoTrafficForDayAsync(DateTime selectedLocalDate, CancellationToken cancellationToken)
    {
        var prefix = $"demo-traffic-v16-{selectedLocalDate:yyyyMMdd}-";
        var startUtc = ToReportUtc(selectedLocalDate);
        var endUtc = ToReportUtc(selectedLocalDate.AddDays(1).AddTicks(-1));

        var alreadySeeded = await _context.VisitorPlaybackLogs
            .AnyAsync(log => log.DeviceId.StartsWith(prefix)
                && log.CreatedAt >= startUtc
                && log.CreatedAt <= endUtc,
                cancellationToken);

        if (alreadySeeded)
            return;

        var nowLocal = ToReportLocal(DateTime.UtcNow);
        var isToday = nowLocal.Date == selectedLocalDate;
        var currentAnchor = isToday
            ? selectedLocalDate.Add(nowLocal.TimeOfDay)
            : selectedLocalDate.AddHours(12).AddMinutes(30);

        var menuPoiIds = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => item.Status != "Hidden")
            .Select(item => item.PoiId)
            .Distinct()
            .Take(6)
            .ToListAsync(cancellationToken);

        var poiIds = menuPoiIds.Count > 0
            ? menuPoiIds
            : await _context.Pois
                .AsNoTracking()
                .Where(poi => poi.Status == "Approved")
                .Select(poi => poi.Id)
                .Take(6)
                .ToListAsync(cancellationToken);

        if (poiIds.Count == 0)
            return;

        var touristIds = await _context.Tourists
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .Take(12)
            .ToListAsync(cancellationToken);

        if (touristIds.Count == 0)
            touristIds.Add(1);

        var random = new Random(selectedLocalDate.ToString("yyyyMMdd").GetHashCode());
        var triggers = new[] { "WebMapOpen", "WebDetailOpen", "QR", "Menu", "WebAudioPlay" };
        var timeSlots = BuildDemoTimeSlots(selectedLocalDate, currentAnchor);

        var logIndex = 1;
        foreach (var poiId in poiIds)
        {
            var visits = random.Next(4, 9);
            for (var i = 0; i < visits; i++)
            {
                var localTime = timeSlots[(i + poiId) % timeSlots.Count].AddMinutes(random.Next(0, 25));
                var touristId = touristIds[random.Next(touristIds.Count)];

                _context.VisitorPlaybackLogs.Add(new VisitorPlaybackLog
                {
                    TouristId = touristId,
                    DeviceId = $"{prefix}{poiId}-{logIndex++}",
                    PoiId = poiId,
                    LanguageCode = "vi",
                    TriggerType = triggers[random.Next(triggers.Length)],
                    VisitorLatitude = null,
                    VisitorLongitude = null,
                    ListenDuration = random.Next(0, 180),
                    CreatedAt = ToReportUtc(localTime)
                });
            }
        }

        var activeMenuItems = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => poiIds.Contains(item.PoiId) && item.Status == "Active")
            .OrderBy(item => item.PoiId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var orderPois = activeMenuItems
            .GroupBy(item => item.PoiId)
            .Select(group => group.First())
            .Take(4)
            .ToList();

        var orderIndex = 1;
        foreach (var item in orderPois)
        {
            var orderCode = $"TRAFFIC-{selectedLocalDate:yyyyMMdd}-{item.PoiId}";
            var exists = await _context.MenuOrders.AnyAsync(order => order.OrderCode == orderCode, cancellationToken);
            if (exists)
                continue;

            var localOrderTime = isToday
                ? currentAnchor.AddMinutes(-Math.Min(25, 5 + orderIndex * 4))
                : selectedLocalDate.AddHours(11 + orderIndex).AddMinutes(10);

            var order = new MenuOrder
            {
                OrderCode = orderCode,
                TouristId = touristIds[(orderIndex - 1) % touristIds.Count],
                OwnerProfileId = item.OwnerProfileId,
                PoiId = item.PoiId,
                CustomerName = "Du khách Demo",
                CustomerPhone = "0909000000",
                Note = "Đơn demo lưu lượng khách theo quán.",
                Status = orderIndex % 2 == 0 ? "Confirmed" : "Pending",
                PaymentMethod = "PayAtCounter",
                PaymentStatus = "Unpaid",
                Subtotal = item.Price,
                TotalAmount = item.Price,
                Currency = "VND",
                CreatedAt = ToReportUtc(localOrderTime)
            };

            order.Items.Add(new MenuOrderItem
            {
                OwnerMenuItemId = item.Id,
                ItemName = item.Name,
                UnitPrice = item.Price,
                Quantity = 1,
                LineTotal = item.Price,
                Currency = "VND"
            });

            _context.MenuOrders.Add(order);
            orderIndex++;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static List<DateTime> BuildDemoTimeSlots(DateTime selectedLocalDate, DateTime currentAnchor)
    {
        var slots = new List<DateTime>
        {
            selectedLocalDate.AddHours(8).AddMinutes(10),
            selectedLocalDate.AddHours(9).AddMinutes(35),
            selectedLocalDate.AddHours(11).AddMinutes(5),
            selectedLocalDate.AddHours(12).AddMinutes(20),
            selectedLocalDate.AddHours(15).AddMinutes(10),
            selectedLocalDate.AddHours(17).AddMinutes(40),
            selectedLocalDate.AddHours(19).AddMinutes(15),
        };

        if (currentAnchor.Date == selectedLocalDate)
        {
            slots.Add(currentAnchor.AddMinutes(-28));
            slots.Add(currentAnchor.AddMinutes(-12));
            slots.Add(currentAnchor.AddMinutes(-4));
        }

        return slots
            .Where(item => item.Date == selectedLocalDate)
            .OrderBy(item => item)
            .ToList();
    }

    private static List<VisitorTrafficEventRowViewModel> BuildRecentEvents(
        IReadOnlyList<VisitorPlaybackLog> logs,
        IReadOnlyList<TouristPoiDiscovery> discoveries,
        IReadOnlyList<MenuOrder> orders,
        IReadOnlyDictionary<int, string> poiNameById)
    {
        var rows = new List<VisitorTrafficEventRowViewModel>();

        rows.AddRange(logs.Select(log => new VisitorTrafficEventRowViewModel
        {
            CreatedAt = ToReportLocal(log.CreatedAt),
            EventType = GetLogEventType(log.TriggerType),
            PoiName = GetPoiName(poiNameById, log.PoiId),
            VisitorLabel = log.TouristId.HasValue ? $"Du khách #{log.TouristId}" : ShortDevice(log.DeviceId),
            Detail = $"Ngôn ngữ {log.LanguageCode?.ToUpperInvariant()} · {NormalizeTriggerType(log.TriggerType)}"
        }));

        rows.AddRange(discoveries.Select(item => new VisitorTrafficEventRowViewModel
        {
            CreatedAt = ToReportLocal(item.DiscoveredAt),
            EventType = "Check-in GPS",
            PoiName = GetPoiName(poiNameById, item.PoiId),
            VisitorLabel = $"Du khách #{item.TouristId}",
            Detail = $"{item.DiscoveryMethod} · +{item.PointsAwarded} điểm"
        }));

        rows.AddRange(orders.Select(order => new VisitorTrafficEventRowViewModel
        {
            CreatedAt = ToReportLocal(order.CreatedAt),
            EventType = "Đơn menu/quán",
            PoiName = GetPoiName(poiNameById, order.PoiId),
            VisitorLabel = string.IsNullOrWhiteSpace(order.CustomerName)
                ? $"Du khách #{order.TouristId}"
                : order.CustomerName,
            Detail = $"{order.OrderCode} · {order.Status} · {order.TotalAmount:N0} {order.Currency}"
        }));

        return rows
            .OrderByDescending(row => row.CreatedAt)
            .Take(30)
            .ToList();
    }

    private static string GetPoiName(IReadOnlyDictionary<int, string> poiNameById, int poiId)
    {
        return poiNameById.TryGetValue(poiId, out var poiName) ? poiName : $"POI #{poiId}";
    }

    private static string GetPoiName(Poi poi)
    {
        return poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")?.Name
            ?? poi.Translations.FirstOrDefault()?.Name
            ?? $"POI #{poi.Id}";
    }

    private static string GetLogEventType(string? triggerType)
    {
        var trigger = triggerType ?? "";

        if (trigger.Contains("Audio", StringComparison.OrdinalIgnoreCase) ||
            trigger.Contains("Tts", StringComparison.OrdinalIgnoreCase))
            return "Nghe thuyết minh";

        if (trigger.Contains("CheckIn", StringComparison.OrdinalIgnoreCase))
            return "Check-in GPS";

        if (trigger.Contains("Menu", StringComparison.OrdinalIgnoreCase))
            return "Mở menu/quán";

        if (trigger.Contains("QR", StringComparison.OrdinalIgnoreCase))
            return "Quét QR";

        if (trigger.Contains("Detail", StringComparison.OrdinalIgnoreCase))
            return "Mở chi tiết POI";

        return "Tương tác bản đồ";
    }

    private static string NormalizeTriggerType(string? triggerType)
    {
        return string.IsNullOrWhiteSpace(triggerType) ? "Không rõ nguồn" : triggerType.Trim();
    }

    private static TimeSpan ParseClock(string? value, TimeSpan fallback)
    {
        return TimeSpan.TryParse(value, out var result) ? result : fallback;
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
        if (log.TouristId.HasValue)
            return TouristKey(log.TouristId.Value);

        if (!string.IsNullOrWhiteSpace(log.DeviceId))
            return $"D:{log.DeviceId.Trim()}";

        return $"LOG:{log.Id}";
    }

    private static string TouristKey(int touristId) => $"T:{touristId}";

    private static string ShortDevice(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return "Thiết bị ẩn danh";

        var trimmed = deviceId.Trim();
        return trimmed.Length <= 8 ? $"Thiết bị {trimmed}" : $"Thiết bị {trimmed[..8]}...";
    }

    private static bool IsClosedOrder(string? status)
    {
        return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCancelledOrder(string? status)
    {
        return string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? Latest(IEnumerable<DateTime?> values)
    {
        return values
            .Where(value => value.HasValue)
            .OrderByDescending(value => value)
            .FirstOrDefault();
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
}
