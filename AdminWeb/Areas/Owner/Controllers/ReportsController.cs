using AdminWeb.Areas.Owner.ViewModels;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdminWeb.Services.Export;
using System.Security.Claims;
using System.Text;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
public sealed class ReportsController : Controller
{
    private const decimal PlatformCommissionRate = 0.10m;

    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(
        string rangePreset = "1m",
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        ViewData["Title"] = "Báo cáo & doanh thu";

        var range = ResolveReportRange(rangePreset, from, to);
        var model = await BuildReportAsync(owner, range, cancellationToken);

        return View(model);
    }

    public async Task<IActionResult> ExportExcel(
        string rangePreset = "1m",
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var range = ResolveReportRange(rangePreset, from, to);
        var model = await BuildReportAsync(owner, range, cancellationToken);

        var workbook = new ExcelWorkbookBuilder()
            .AddSheet("Tổng quan", new[]
            {
                new object?[] { "Báo cáo chủ cửa hàng", owner.BusinessName },
                new object?[] { "Khoảng thời gian", model.RangeLabel },
                new object?[] { "Từ", model.RangeStart.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") },
                new object?[] { "Đến", model.RangeEnd.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") },
                Array.Empty<object?>(),
                new object?[] { "Chỉ số", "Giá trị" },
                new object?[] { "POI", model.PoiCount },
                new object?[] { "Menu/sản phẩm", model.MenuItemCount },
                new object?[] { "Tổng đơn", model.TotalOrderCount },
                new object?[] { "Đơn hoàn tất", model.CompletedOrderCount },
                new object?[] { "Đơn đang xử lý", model.PendingOrderCount },
                new object?[] { "Đơn hủy", model.CancelledOrderCount },
                new object?[] { "GMV đơn hàng", model.RangeOrderRevenue },
                new object?[] { "Doanh thu đơn hoàn tất", model.CompletedOrderRevenue },
                new object?[] { "Doanh số đã thanh toán", model.PaidOrderRevenue },
                new object?[] { "Phí nền tảng", model.PlatformCommissionAmount },
                new object?[] { "Tỷ lệ phí nền tảng", model.PlatformCommissionRate },
                new object?[] { "Chủ cửa hàng thực nhận", model.OwnerNetRevenue },
                new object?[] { "Giá trị đơn đang xử lý", model.PendingOrderAmount },
                new object?[] { "Gói đã thanh toán cho nền tảng", model.PaidRevenue },
                new object?[] { "Gói đang chờ", model.PendingPayments },
                new object?[] { "Lượt mở POI", model.TotalPlayCount },
                new object?[] { "Lượt QR", model.QrScanCount },
                new object?[] { "Đánh giá", model.TotalReviewCount },
                new object?[] { "Điểm trung bình", Math.Round(model.AverageRating, 1) }
            })
            .AddSheet("Doanh thu theo ngày", BuildOwnerRevenueTimelineRows(model.RevenueLast14Days))
            .AddSheet("Đơn hàng", BuildOwnerOrderRows(model.RecentOrders))
            .AddSheet("Món bán chạy", BuildOwnerTopMenuRows(model.TopMenuItems))
            .AddSheet("POI nổi bật", BuildOwnerPoiRows(model.TopPois))
            .AddSheet("Đánh giá", BuildOwnerReviewRows(model.RecentReviews));

        var safeBusinessName = ToSafeFileName(owner.BusinessName);
        var fileName = $"BaoCao_ChuCuaHang_{safeBusinessName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(
            workbook.Build(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }



    private static IEnumerable<IEnumerable<object?>> BuildOwnerRevenueTimelineRows(IEnumerable<OwnerRevenueDayRow> rows)
    {
        yield return new object?[] { "Thời gian", "GMV", "Phí nền tảng", "Thực nhận", "Số đơn" };
        foreach (var row in rows)
            yield return new object?[] { row.Label, row.Revenue, row.CommissionAmount, row.NetRevenue, row.OrderCount };
    }

    private static IEnumerable<IEnumerable<object?>> BuildOwnerOrderRows(IEnumerable<MenuOrder> rows)
    {
        yield return new object?[] { "Mã đơn", "Thời gian", "Khách hàng", "Số điện thoại", "POI", "Trạng thái", "Thanh toán", "GMV", "Phí nền tảng", "Thực nhận", "Ghi chú" };
        foreach (var order in rows)
        {
            var commission = IsPaidMenuOrder(order) ? CalculateCommission(order.TotalAmount) : 0;
            var ownerNet = IsPaidMenuOrder(order) ? Math.Max(0, order.TotalAmount - commission) : 0;

            yield return new object?[]
            {
                order.OrderCode,
                order.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                order.CustomerName,
                order.CustomerPhone,
                GetOrderPoiName(order),
                order.Status,
                order.PaymentStatus,
                order.TotalAmount,
                commission,
                ownerNet,
                order.Note ?? ""
            };
        }
    }

    private static IEnumerable<IEnumerable<object?>> BuildOwnerTopMenuRows(IEnumerable<OwnerTopMenuItemRow> rows)
    {
        yield return new object?[] { "Tên món", "Số lượng", "Doanh thu" };
        foreach (var row in rows)
            yield return new object?[] { row.ItemName, row.Quantity, row.Revenue };
    }

    private static IEnumerable<IEnumerable<object?>> BuildOwnerPoiRows(IEnumerable<OwnerReportPoiRow> rows)
    {
        yield return new object?[] { "POI", "Trạng thái", "Lượt mở", "Đánh giá", "Điểm TB" };
        foreach (var row in rows)
            yield return new object?[] { row.Name, row.Status, row.PlayCount, row.ReviewCount, Math.Round(row.AverageRating, 1) };
    }

    private static IEnumerable<IEnumerable<object?>> BuildOwnerReviewRows(IEnumerable<PoiReview> rows)
    {
        yield return new object?[] { "Thời gian", "POI", "Điểm", "Bình luận" };
        foreach (var review in rows)
        {
            yield return new object?[]
            {
                review.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                review.Poi == null ? $"POI #{review.PoiId}" : GetPoiName(review.Poi),
                review.Rating,
                review.Comment ?? ""
            };
        }
    }

    private static string ToSafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string((value ?? "Owner").Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Owner" : cleaned;
    }

    private async Task<OwnerReportViewModel> BuildReportAsync(
        OwnerProfile owner,
        ReportRange range,
        CancellationToken cancellationToken)
    {
        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Where(poi => poi.OwnerProfileId == owner.Id)
            .ToListAsync(cancellationToken);

        var poiIds = pois.Select(poi => poi.Id).ToList();

        var model = new OwnerReportViewModel
        {
            Owner = owner,
            PoiCount = pois.Count,
            RangePreset = range.Preset,
            RangeStart = range.Start,
            RangeEnd = range.End,
            CustomStart = range.CustomStart,
            CustomEnd = range.CustomEnd,
            RangeLabel = range.Label
        };

        model.MenuItemCount = await _context.OwnerMenuItems
            .AsNoTracking()
            .CountAsync(item => item.OwnerProfileId == owner.Id, cancellationToken);

        var payments = await _context.PaymentTransactions
            .AsNoTracking()
            .Where(payment =>
                payment.OwnerProfileId == owner.Id &&
                payment.CreatedAt >= range.Start &&
                payment.CreatedAt <= range.End)
            .ToListAsync(cancellationToken);

        model.PaidRevenue = payments
            .Where(payment => IsPaidTransaction(payment.Status))
            .Sum(payment => payment.Amount);
        model.PendingPayments = payments.Count(payment => IsPendingTransaction(payment.Status));

        var orders = await _context.MenuOrders
            .AsNoTracking()
            .Include(order => order.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Include(order => order.Items)
            .Where(order =>
                order.OwnerProfileId == owner.Id &&
                order.CreatedAt >= range.Start &&
                order.CreatedAt <= range.End)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

        var nonCancelledOrders = orders.Where(order => !IsCancelledMenuOrder(order)).ToList();
        var completedOrders = orders.Where(IsCompletedMenuOrder).ToList();
        var paidOrders = orders.Where(IsPaidMenuOrder).ToList();
        var processingOrders = orders.Where(IsPendingMenuOrder).ToList();
        var paidOrderRevenue = paidOrders.Sum(order => order.TotalAmount);
        var platformCommissionAmount = CalculateCommission(paidOrderRevenue);

        model.PlatformCommissionRate = PlatformCommissionRate;
        model.TotalOrderCount = orders.Count;
        model.CompletedOrderCount = completedOrders.Count;
        model.PendingOrderCount = processingOrders.Count;
        model.CancelledOrderCount = orders.Count(IsCancelledMenuOrder);
        model.RangeOrderRevenue = nonCancelledOrders.Sum(order => order.TotalAmount);
        model.CompletedOrderRevenue = completedOrders.Sum(order => order.TotalAmount);
        model.PaidOrderRevenue = paidOrderRevenue;
        model.PlatformCommissionAmount = platformCommissionAmount;
        model.OwnerNetRevenue = Math.Max(0, paidOrderRevenue - platformCommissionAmount);
        model.PendingOrderAmount = processingOrders.Sum(order => order.TotalAmount);
        model.TodayOrderRevenue = model.RangeOrderRevenue;
        model.Last30DaysOrderRevenue = model.RangeOrderRevenue;
        model.RecentOrders = orders;
        model.TopMenuItems = orders
            .Where(order => !IsCancelledMenuOrder(order))
            .SelectMany(order => order.Items)
            .GroupBy(item => item.ItemName)
            .Select(group => new OwnerTopMenuItemRow
            {
                ItemName = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                Revenue = group.Sum(item => item.LineTotal)
            })
            .OrderByDescending(row => row.Revenue)
            .ToList();
        model.RevenueLast14Days = BuildRevenueTimeline(range, nonCancelledOrders);

        var playbackLogs = new List<PlaybackReportLog>();
        var reviewRows = new List<PoiReview>();

        if (poiIds.Count > 0)
        {
            playbackLogs = await _context.VisitorPlaybackLogs
                .AsNoTracking()
                .Where(log =>
                    poiIds.Contains(log.PoiId) &&
                    log.CreatedAt >= range.Start &&
                    log.CreatedAt <= range.End)
                .Select(log => new PlaybackReportLog(log.PoiId, log.TriggerType, log.CreatedAt))
                .ToListAsync(cancellationToken);

            reviewRows = await _context.PoiReviews
                .AsNoTracking()
                .Include(review => review.Poi)
                    .ThenInclude(poi => poi!.Translations)
                .Where(review =>
                    poiIds.Contains(review.PoiId) &&
                    review.CreatedAt >= range.Start &&
                    review.CreatedAt <= range.End)
                .OrderByDescending(review => review.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        model.TotalPlayCount = playbackLogs.Count;
        model.QrScanCount = playbackLogs.Count(log => IsQrTrigger(log.TriggerType));
        model.TotalReviewCount = reviewRows.Count;
        model.AverageRating = reviewRows.Count == 0 ? 0 : reviewRows.Average(review => review.Rating);
        model.RecentReviews = reviewRows;

        var playByPoi = playbackLogs
            .GroupBy(log => log.PoiId)
            .ToDictionary(group => group.Key, group => group.Count());

        var reviewByPoi = reviewRows
            .GroupBy(review => review.PoiId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Count = group.Count(),
                    Average = group.Average(review => review.Rating)
                });

        model.TopPois = pois
            .Select(poi => new OwnerReportPoiRow
            {
                PoiId = poi.Id,
                Name = GetPoiName(poi),
                Status = poi.Status,
                PlayCount = playByPoi.TryGetValue(poi.Id, out var playCount) ? playCount : 0,
                ReviewCount = reviewByPoi.TryGetValue(poi.Id, out var reviewInfo) ? reviewInfo.Count : 0,
                AverageRating = reviewByPoi.TryGetValue(poi.Id, out reviewInfo) ? reviewInfo.Average : 0
            })
            .OrderByDescending(row => row.PlayCount)
            .ThenByDescending(row => row.AverageRating)
            .ToList();

        model.Last14Days = BuildPlaybackTimeline(range, playbackLogs);

        return model;
    }

    private static ReportRange ResolveReportRange(string? rangePreset, DateTime? from, DateTime? to)
    {
        var hasCustomDateFilter = from.HasValue || to.HasValue;
        var preset = hasCustomDateFilter
            ? "custom"
            : string.IsNullOrWhiteSpace(rangePreset)
                ? "1m"
                : rangePreset.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        if (preset == "custom")
        {
            var localNow = now.ToLocalTime();
            var startDate = (from ?? to ?? localNow).Date;
            var endDate = (to ?? from ?? localNow).Date;

            if (endDate < startDate)
                endDate = startDate;

            return new ReportRange(
                "custom",
                LocalDateStartToUtc(startDate),
                LocalDateEndToUtc(endDate),
                startDate,
                endDate,
                $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}");
        }

        return preset switch
        {
            "24h" => new ReportRange("24h", now.AddHours(-24), now, null, null, "24h gần nhất"),
            "7d" or "1w" => new ReportRange("7d", now.AddDays(-7), now, null, null, "7 ngày gần nhất"),
            "2m" => new ReportRange("2m", now.AddMonths(-2), now, null, null, "2 tháng gần nhất"),
            _ => new ReportRange("1m", now.AddMonths(-1), now, null, null, "1 tháng gần nhất")
        };
    }

    private static List<OwnerRevenueDayRow> BuildRevenueTimeline(ReportRange range, IReadOnlyCollection<MenuOrder> orders)
    {
        if (UseHourlyBuckets(range))
        {
            var endLocal = range.End.ToLocalTime();
            var endHour = new DateTime(endLocal.Year, endLocal.Month, endLocal.Day, endLocal.Hour, 0, 0, DateTimeKind.Local);
            var startHour = endHour.AddHours(-23);

            return Enumerable.Range(0, 24)
                .Select(offset => startHour.AddHours(offset))
                .Select(hour =>
                {
                    var hourUtc = hour.ToUniversalTime();
                    var nextHourUtc = hour.AddHours(1).ToUniversalTime();
                    var bucket = orders
                        .Where(order => order.CreatedAt >= hourUtc && order.CreatedAt < nextHourUtc)
                        .ToList();

                    var paidRevenue = bucket.Where(IsPaidMenuOrder).Sum(order => order.TotalAmount);
                    var commission = CalculateCommission(paidRevenue);

                    return new OwnerRevenueDayRow
                    {
                        Date = hour,
                        Label = hour.ToString("HH:mm"),
                        Revenue = bucket.Sum(order => order.TotalAmount),
                        CommissionAmount = commission,
                        NetRevenue = Math.Max(0, paidRevenue - commission),
                        OrderCount = bucket.Count
                    };
                })
                .ToList();
        }

        var startDate = range.Start.ToLocalTime().Date;
        var days = Math.Max(1, (range.End.ToLocalTime().Date - startDate).Days + 1);

        return Enumerable.Range(0, days)
            .Select(offset => startDate.AddDays(offset))
            .Select(date =>
            {
                var startUtc = DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
                var endUtc = DateTime.SpecifyKind(date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                var bucket = orders
                    .Where(order => order.CreatedAt >= startUtc && order.CreatedAt < endUtc)
                    .ToList();

                var paidRevenue = bucket.Where(IsPaidMenuOrder).Sum(order => order.TotalAmount);
                var commission = CalculateCommission(paidRevenue);

                return new OwnerRevenueDayRow
                {
                    Date = date,
                    Label = date.ToString("dd/MM"),
                    Revenue = bucket.Sum(order => order.TotalAmount),
                    CommissionAmount = commission,
                    NetRevenue = Math.Max(0, paidRevenue - commission),
                    OrderCount = bucket.Count
                };
            })
            .ToList();
    }

    private static List<OwnerReportDayRow> BuildPlaybackTimeline(ReportRange range, IReadOnlyCollection<PlaybackReportLog> logs)
    {
        if (UseHourlyBuckets(range))
        {
            var endLocal = range.End.ToLocalTime();
            var endHour = new DateTime(endLocal.Year, endLocal.Month, endLocal.Day, endLocal.Hour, 0, 0, DateTimeKind.Local);
            var startHour = endHour.AddHours(-23);

            return Enumerable.Range(0, 24)
                .Select(offset => startHour.AddHours(offset))
                .Select(hour =>
                {
                    var hourUtc = hour.ToUniversalTime();
                    var nextHourUtc = hour.AddHours(1).ToUniversalTime();
                    var bucket = logs
                        .Where(log => log.CreatedAt >= hourUtc && log.CreatedAt < nextHourUtc)
                        .ToList();

                    return new OwnerReportDayRow
                    {
                        Date = hour,
                        Label = hour.ToString("HH:mm"),
                        PlayCount = bucket.Count,
                        QrCount = bucket.Count(log => IsQrTrigger(log.TriggerType))
                    };
                })
                .ToList();
        }

        var startDate = range.Start.ToLocalTime().Date;
        var days = Math.Max(1, (range.End.ToLocalTime().Date - startDate).Days + 1);

        return Enumerable.Range(0, days)
            .Select(offset => startDate.AddDays(offset))
            .Select(date =>
            {
                var startUtc = DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
                var endUtc = DateTime.SpecifyKind(date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                var bucket = logs
                    .Where(log => log.CreatedAt >= startUtc && log.CreatedAt < endUtc)
                    .ToList();

                return new OwnerReportDayRow
                {
                    Date = date,
                    Label = date.ToString("dd/MM"),
                    PlayCount = bucket.Count,
                    QrCount = bucket.Count(log => IsQrTrigger(log.TriggerType))
                };
            })
            .ToList();
    }

    private static bool IsPaidMenuOrder(MenuOrder order)
    {
        // Đơn hoàn tất cũng được tính là đã thu tiền để dữ liệu demo/cũ không bị phí = 0.
        return !IsCancelledMenuOrder(order) &&
               (IsCompletedMenuOrder(order) || IsPaidPaymentStatus(order.PaymentStatus));
    }

    private static bool IsCompletedMenuOrder(MenuOrder order)
    {
        return IsAny(order.Status,
            "Completed", "Complete", "Done", "Delivered", "Paid",
            "HoanThanh", "Hoàn thành", "DaHoanThanh", "Đã hoàn thành");
    }

    private static bool IsCancelledMenuOrder(MenuOrder order)
    {
        return IsAny(order.Status,
            "Cancelled", "Canceled", "Cancel",
            "Huy", "Huỷ", "DaHuy", "Đã hủy", "Đã huỷ");
    }

    private static bool IsPendingMenuOrder(MenuOrder order)
    {
        return !IsCancelledMenuOrder(order) && !IsCompletedMenuOrder(order);
    }

    private static bool IsPaidPaymentStatus(string? paymentStatus)
    {
        return IsAny(paymentStatus,
            "Paid", "Success", "Completed", "Confirmed",
            "DaThanhToan", "Đã thanh toán", "Thanh toán", "Da thu tien", "Đã thu tiền");
    }

    private static bool IsPaidTransaction(string? status)
    {
        return IsAny(status, "Paid", "Success", "Completed", "Confirmed", "DaThanhToan", "Đã thanh toán");
    }

    private static bool IsPendingTransaction(string? status)
    {
        return IsAny(status, "Pending", "Waiting", "Processing", "ChoThanhToan", "Chờ thanh toán");
    }

    private static bool IsAny(string? value, params string[] expectedValues)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = NormalizeStatusText(value);
        return expectedValues.Any(expected => normalized == NormalizeStatusText(expected));
    }

    private static string NormalizeStatusText(string value)
    {
        return value.Trim()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static decimal CalculateCommission(decimal amount)
    {
        return Math.Round(amount * PlatformCommissionRate, 0, MidpointRounding.AwayFromZero);
    }

    private static DateTime LocalDateStartToUtc(DateTime localDate)
    {
        return DateTime.SpecifyKind(localDate.Date, DateTimeKind.Local).ToUniversalTime();
    }

    private static DateTime LocalDateEndToUtc(DateTime localDate)
    {
        return DateTime.SpecifyKind(localDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();
    }

    private static bool UseHourlyBuckets(ReportRange range)
    {
        return range.Preset == "24h";
    }

    private static string GetPoiName(Poi poi)
    {
        return poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")?.Name
            ?? poi.Translations.FirstOrDefault()?.Name
            ?? $"POI #{poi.Id}";
    }

    private static string GetOrderPoiName(MenuOrder order)
    {
        return order.Poi?.Translations.FirstOrDefault(item => item.LanguageCode == "vi")?.Name
            ?? order.Poi?.Translations.FirstOrDefault()?.Name
            ?? $"POI #{order.PoiId}";
    }

    private static bool IsQrTrigger(string? triggerType)
    {
        return !string.IsNullOrWhiteSpace(triggerType) &&
               triggerType.Contains("QR", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendCsvLine(StringBuilder builder, params object?[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(value => ToCsvValue(value?.ToString() ?? ""))));
    }

    private static string ToCsvValue(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private async Task<OwnerProfile?> GetOwnerAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.Identity?.Name;

        return await _context.OwnerProfiles
            .Include(owner => owner.User)
            .FirstOrDefaultAsync(owner =>
                (userId != null && owner.UserId.ToString() == userId) ||
                owner.User!.Username == username,
                cancellationToken);
    }

    private sealed record ReportRange(
        string Preset,
        DateTime Start,
        DateTime End,
        DateTime? CustomStart,
        DateTime? CustomEnd,
        string Label);

    private sealed record PlaybackReportLog(int PoiId, string? TriggerType, DateTime CreatedAt);
}
