using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdminWeb.Services.Export;
using System.Globalization;
using System.Text;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class RevenueController : Controller
{
    private const decimal PlatformCommissionRate = 0.10m;

    private readonly AppDbContext _context;

    public RevenueController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string rangePreset = "1m",
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveReportRange(rangePreset, from, to);
        var model = await BuildDashboardAsync(range, cancellationToken, rowLimit: 10);

        ViewData["Title"] = "Quản lý doanh thu";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(
        string rangePreset = "1m",
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveReportRange(rangePreset, from, to);
        var model = await BuildDashboardAsync(range, cancellationToken, rowLimit: null);

        var workbook = new ExcelWorkbookBuilder()
            .AddSheet("Tổng quan", new[]
            {
                new object?[] { "Báo cáo doanh thu Admin" },
                new object?[] { "Khoảng thời gian", model.RangeLabel },
                new object?[] { "Từ", model.RangeStart.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") },
                new object?[] { "Đến", model.RangeEnd.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") },
                Array.Empty<object?>(),
                new object?[] { "Chỉ số", "Giá trị" },
                new object?[] { "Doanh thu nền tảng", model.PlatformRevenueTotal },
                new object?[] { "Doanh thu gói", model.PlatformSubscriptionRevenue },
                new object?[] { "Gói Owner", model.OwnerSubscriptionRevenue },
                new object?[] { "Gói Du khách", model.TouristSubscriptionRevenue },
                new object?[] { "Hoa hồng đơn hàng", model.PlatformCommissionRevenue },
                new object?[] { "Tỷ lệ hoa hồng", model.PlatformCommissionRate },
                new object?[] { "GMV đơn đồ ăn", model.GrossMenuSales },
                new object?[] { "Doanh số menu hoàn tất", model.CompletedMenuSales },
                new object?[] { "Doanh số menu đã thanh toán", model.PaidMenuSales },
                new object?[] { "Chủ cửa hàng thực nhận", model.OwnerNetMenuRevenue },
                new object?[] { "Giá trị đơn đang xử lý", model.PendingMenuSales },
                new object?[] { "Giao dịch gói đã trả", model.PaidPaymentCount },
                new object?[] { "Giao dịch gói chờ", model.PendingPaymentCount },
                new object?[] { "Đơn menu hoàn tất", model.CompletedOrderCount },
                new object?[] { "Đơn menu đang xử lý", model.PendingOrderCount },
                new object?[] { "Đơn menu hủy", model.CancelledOrderCount }
            })
            .AddSheet("Doanh thu theo ngày", BuildAdminTimelineRows(model.Last14Days))
            .AddSheet("Top chủ cửa hàng", BuildAdminTopOwnerRows(model.TopOwners))
            .AddSheet("Đơn hàng menu", BuildAdminOrderRows(model.RecentOrders))
            .AddSheet("Giao dịch gói", BuildAdminPaymentRows(model.RecentPayments));

        var fileName = $"BaoCao_Admin_DoanhThu_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(
            workbook.Build(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }



    private static IEnumerable<IEnumerable<object?>> BuildAdminTimelineRows(IEnumerable<AdminRevenueDayRow> rows)
    {
        yield return new object?[] { "Thời gian", "Doanh thu nền tảng", "Doanh thu gói", "Hoa hồng", "GMV menu", "Chủ cửa hàng thực nhận", "Số đơn menu" };
        foreach (var row in rows)
            yield return new object?[] { row.Label, row.PlatformRevenue, row.SubscriptionRevenue, row.CommissionRevenue, row.MenuSales, row.OwnerNetSales, row.Orders };
    }

    private static IEnumerable<IEnumerable<object?>> BuildAdminTopOwnerRows(IEnumerable<AdminRevenueOwnerRow> rows)
    {
        yield return new object?[] { "Chủ cửa hàng", "Đơn hoàn tất", "GMV", "Doanh số đã thanh toán", "Hoa hồng nền tảng", "Chủ cửa hàng thực nhận", "Gói đã trả" };
        foreach (var row in rows)
            yield return new object?[] { row.BusinessName, row.CompletedOrders, row.GrossSales, row.PaidSales, row.CommissionAmount, row.OwnerNetSales, row.SubscriptionPaid };
    }

    private static IEnumerable<IEnumerable<object?>> BuildAdminOrderRows(IEnumerable<MenuOrder> rows)
    {
        yield return new object?[] { "Mã đơn", "Thời gian", "Chủ cửa hàng", "POI", "Trạng thái", "Thanh toán", "GMV", "Phí nền tảng", "Chủ cửa hàng thực nhận" };
        foreach (var order in rows)
        {
            var commission = IsPaidMenuOrder(order) ? CalculateCommission(order.TotalAmount) : 0;
            var ownerNet = IsPaidMenuOrder(order) ? Math.Max(0, order.TotalAmount - commission) : 0;

            yield return new object?[]
            {
                order.OrderCode,
                order.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                order.OwnerProfile?.BusinessName ?? $"Owner #{order.OwnerProfileId}",
                GetOrderPoiName(order),
                order.Status,
                order.PaymentStatus,
                order.TotalAmount,
                commission,
                ownerNet
            };
        }
    }

    private static IEnumerable<IEnumerable<object?>> BuildAdminPaymentRows(IEnumerable<PaymentTransaction> rows)
    {
        yield return new object?[] { "Mã giao dịch", "Thời gian trả", "Người trả", "Loại", "Gói", "Cổng", "Số tiền" };
        foreach (var payment in rows)
        {
            yield return new object?[]
            {
                payment.TransactionCode,
                payment.PaidAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                GetPaymentCustomerName(payment),
                payment.PayerType,
                payment.PaymentPlan?.PlanName ?? payment.Purpose,
                payment.PaymentMethod,
                payment.Amount
            };
        }
    }

    private async Task<AdminRevenueDashboardViewModel> BuildDashboardAsync(
        ReportRange range,
        CancellationToken cancellationToken,
        int? rowLimit)
    {
        var paidPayments = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(payment => payment.OwnerProfile)
                .ThenInclude(owner => owner!.User)
            .Include(payment => payment.Tourist)
            .Include(payment => payment.PaymentPlan)
            .Where(payment =>
                (payment.Status == "Paid" || payment.Status == "Completed" || payment.Status == "Success" || payment.Status == "Confirmed") &&
                payment.PaidAt.HasValue &&
                payment.PaidAt.Value >= range.Start &&
                payment.PaidAt.Value <= range.End)
            .OrderByDescending(payment => payment.PaidAt)
            .ToListAsync(cancellationToken);

        var pendingPaymentCount = await _context.PaymentTransactions
            .AsNoTracking()
            .CountAsync(payment =>
                (payment.Status == "Pending" || payment.Status == "Waiting" || payment.Status == "Processing") &&
                payment.CreatedAt >= range.Start &&
                payment.CreatedAt <= range.End,
                cancellationToken);

        var orders = await _context.MenuOrders
            .AsNoTracking()
            .Include(order => order.OwnerProfile)
            .Include(order => order.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Include(order => order.Items)
            .Where(order => order.CreatedAt >= range.Start && order.CreatedAt <= range.End)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

        var nonCancelledOrders = orders.Where(order => !IsCancelledMenuOrder(order)).ToList();
        var completedOrders = orders.Where(IsCompletedMenuOrder).ToList();
        var paidOrders = orders.Where(IsPaidMenuOrder).ToList();
        var subscriptionRevenue = paidPayments.Sum(payment => payment.Amount);
        var ownerSubscriptionRevenue = paidPayments.Where(payment => payment.PayerType == "Owner").Sum(payment => payment.Amount);
        var touristSubscriptionRevenue = paidPayments.Where(payment => payment.PayerType == "Tourist").Sum(payment => payment.Amount);
        var paidMenuSales = paidOrders.Sum(order => order.TotalAmount);
        var platformCommissionRevenue = CalculateCommission(paidMenuSales);

        var model = new AdminRevenueDashboardViewModel
        {
            FromDate = range.CustomStart ?? range.Start.ToLocalTime().Date,
            ToDate = range.CustomEnd ?? range.End.ToLocalTime().Date,
            RangePreset = range.Preset,
            RangeStart = range.Start,
            RangeEnd = range.End,
            CustomStart = range.CustomStart,
            CustomEnd = range.CustomEnd,
            RangeLabel = range.Label,
            PlatformCommissionRate = PlatformCommissionRate,
            PlatformSubscriptionRevenue = subscriptionRevenue,
            OwnerSubscriptionRevenue = ownerSubscriptionRevenue,
            TouristSubscriptionRevenue = touristSubscriptionRevenue,
            PlatformCommissionRevenue = platformCommissionRevenue,
            PlatformRevenueTotal = subscriptionRevenue + platformCommissionRevenue,
            GrossMenuSales = nonCancelledOrders.Sum(order => order.TotalAmount),
            CompletedMenuSales = completedOrders.Sum(order => order.TotalAmount),
            PaidMenuSales = paidMenuSales,
            OwnerNetMenuRevenue = Math.Max(0, paidMenuSales - platformCommissionRevenue),
            PendingMenuSales = orders.Where(IsPendingMenuOrder).Sum(order => order.TotalAmount),
            PaidPaymentCount = paidPayments.Count,
            PendingPaymentCount = pendingPaymentCount,
            CompletedOrderCount = completedOrders.Count,
            PendingOrderCount = orders.Count(IsPendingMenuOrder),
            CancelledOrderCount = orders.Count(IsCancelledMenuOrder),
            RecentPayments = TakeRows(paidPayments, rowLimit),
            RecentOrders = TakeRows(orders, rowLimit)
        };

        var ownerSubscriptionMap = paidPayments
            .Where(payment => payment.PayerType == "Owner" && payment.OwnerProfileId.HasValue)
            .GroupBy(payment => payment.OwnerProfileId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));

        model.TopOwners = nonCancelledOrders
            .GroupBy(order => new
            {
                order.OwnerProfileId,
                BusinessName = order.OwnerProfile?.BusinessName ?? $"Owner #{order.OwnerProfileId}"
            })
            .Select(group =>
            {
                var ownerPaidSales = group.Where(IsPaidMenuOrder).Sum(order => order.TotalAmount);
                var ownerCommission = CalculateCommission(ownerPaidSales);

                return new AdminRevenueOwnerRow
                {
                    OwnerProfileId = group.Key.OwnerProfileId,
                    BusinessName = group.Key.BusinessName,
                    CompletedOrders = group.Count(IsCompletedMenuOrder),
                    GrossSales = group.Sum(order => order.TotalAmount),
                    PaidSales = ownerPaidSales,
                    CommissionAmount = ownerCommission,
                    OwnerNetSales = Math.Max(0, ownerPaidSales - ownerCommission),
                    SubscriptionPaid = ownerSubscriptionMap.TryGetValue(group.Key.OwnerProfileId, out var subPaid) ? subPaid : 0
                };
            })
            .OrderByDescending(row => row.PaidSales)
            .ThenByDescending(row => row.GrossSales)
            .Take(10)
            .ToList();

        model.Last14Days = BuildRevenueTimeline(range, paidPayments, nonCancelledOrders);

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

    private static List<AdminRevenueDayRow> BuildRevenueTimeline(
        ReportRange range,
        IReadOnlyCollection<PaymentTransaction> payments,
        IReadOnlyCollection<MenuOrder> orders)
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
                    var paymentsOfHour = payments
                        .Where(payment => payment.PaidAt >= hourUtc && payment.PaidAt < nextHourUtc)
                        .ToList();
                    var ordersOfHour = orders
                        .Where(order => order.CreatedAt >= hourUtc && order.CreatedAt < nextHourUtc)
                        .ToList();

                    var subscriptionOfHour = paymentsOfHour.Sum(payment => payment.Amount);
                    var paidMenuOfHour = ordersOfHour.Where(IsPaidMenuOrder).Sum(order => order.TotalAmount);
                    var commissionOfHour = CalculateCommission(paidMenuOfHour);

                    return new AdminRevenueDayRow
                    {
                        Date = hour,
                        Label = hour.ToString("HH:mm"),
                        PlatformRevenue = subscriptionOfHour + commissionOfHour,
                        SubscriptionRevenue = subscriptionOfHour,
                        CommissionRevenue = commissionOfHour,
                        MenuSales = ordersOfHour.Sum(order => order.TotalAmount),
                        OwnerNetSales = Math.Max(0, paidMenuOfHour - commissionOfHour),
                        Orders = ordersOfHour.Count
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
                var paymentsOfDay = payments
                    .Where(payment => payment.PaidAt >= startUtc && payment.PaidAt < endUtc)
                    .ToList();
                var ordersOfDay = orders
                    .Where(order => order.CreatedAt >= startUtc && order.CreatedAt < endUtc)
                    .ToList();

                var subscriptionOfDay = paymentsOfDay.Sum(payment => payment.Amount);
                var paidMenuOfDay = ordersOfDay.Where(IsPaidMenuOrder).Sum(order => order.TotalAmount);
                var commissionOfDay = CalculateCommission(paidMenuOfDay);

                return new AdminRevenueDayRow
                {
                    Date = date,
                    Label = date.ToString("dd/MM"),
                    PlatformRevenue = subscriptionOfDay + commissionOfDay,
                    SubscriptionRevenue = subscriptionOfDay,
                    CommissionRevenue = commissionOfDay,
                    MenuSales = ordersOfDay.Sum(order => order.TotalAmount),
                    OwnerNetSales = Math.Max(0, paidMenuOfDay - commissionOfDay),
                    Orders = ordersOfDay.Count
                };
            })
            .ToList();
    }

    private static bool IsPaidMenuOrder(MenuOrder order)
    {
        // Tính hoa hồng cho đơn không bị hủy và đã hoàn tất hoặc đã thu tiền.
        // Một số đơn demo/cũ chỉ có Status = Completed nhưng PaymentStatus vẫn Unpaid,
        // nên phải tính cả Completed để không bị hoa hồng = 0 dù đơn đã hoàn tất.
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

    private static List<T> TakeRows<T>(IEnumerable<T> rows, int? rowLimit)
    {
        return rowLimit.HasValue ? rows.Take(rowLimit.Value).ToList() : rows.ToList();
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

    private static string GetOrderPoiName(MenuOrder order)
    {
        return order.Poi?.Translations.FirstOrDefault(item => item.LanguageCode == "vi")?.Name
            ?? order.Poi?.Translations.FirstOrDefault()?.Name
            ?? $"POI #{order.PoiId}";
    }

    private static string GetPaymentCustomerName(PaymentTransaction payment)
    {
        return payment.PayerType == "Owner"
            ? payment.OwnerProfile?.BusinessName ?? payment.OwnerProfile?.User?.Username ?? "Owner"
            : payment.Tourist?.FullName ?? payment.Tourist?.Email ?? "Du khách";
    }

    private static void AppendCsvLine(StringBuilder builder, params object?[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(value => ToCsvValue(value?.ToString() ?? ""))));
    }

    private static string ToCsvValue(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private sealed record ReportRange(
        string Preset,
        DateTime Start,
        DateTime End,
        DateTime? CustomStart,
        DateTime? CustomEnd,
        string Label);
}
