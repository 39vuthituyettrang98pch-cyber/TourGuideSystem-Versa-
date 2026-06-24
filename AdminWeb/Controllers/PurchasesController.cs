using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class PurchasesController : Controller
{
    private readonly AppDbContext _context;

    public PurchasesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? status, string? payerType, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var fromDate = (from ?? today.AddDays(-29)).Date;
        var toDate = (to ?? today).Date;
        if (toDate < fromDate)
            (fromDate, toDate) = (toDate, fromDate);

        var fromUtc = fromDate;
        var toExclusive = toDate.AddDays(1);
        status = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim();
        payerType = string.IsNullOrWhiteSpace(payerType) ? "All" : payerType.Trim();

        var transactions = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(payment => payment.OwnerProfile)
                .ThenInclude(owner => owner!.User)
            .Include(payment => payment.Tourist)
            .Include(payment => payment.PaymentPlan)
            .Where(payment =>
                (payment.CreatedAt >= fromUtc && payment.CreatedAt < toExclusive)
                || (payment.PaidAt.HasValue && payment.PaidAt.Value >= fromUtc && payment.PaidAt.Value < toExclusive))
            .ToListAsync(cancellationToken);

        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            transactions = transactions.Where(payment => string.Equals(payment.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.Equals(payerType, "All", StringComparison.OrdinalIgnoreCase))
            transactions = transactions.Where(payment => string.Equals(payment.PayerType, payerType, StringComparison.OrdinalIgnoreCase)).ToList();

        var paid = transactions.Where(payment => payment.Status == "Paid").ToList();
        var ownerTransactions = transactions.Where(payment => payment.PayerType == "Owner").ToList();
        var touristTransactions = transactions.Where(payment => payment.PayerType == "Tourist").ToList();
        var ownerPaid = paid.Where(payment => payment.PayerType == "Owner").ToList();
        var touristPaid = paid.Where(payment => payment.PayerType == "Tourist").ToList();

        var activeOwnerSubscriptions = await _context.OwnerSubscriptions
            .AsNoTracking()
            .Include(subscription => subscription.PaymentPlan)
            .Where(subscription => subscription.Status == "Active" && subscription.ExpiresAt >= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var activeTouristSubscriptions = await _context.TouristSubscriptions
            .AsNoTracking()
            .Include(subscription => subscription.PaymentPlan)
            .Where(subscription => subscription.Status == "Active" && subscription.ExpiresAt >= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var model = new AdminPurchaseDashboardViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalPurchases = transactions.Count,
            PaidPurchases = paid.Count,
            PendingPurchases = transactions.Count(payment => payment.Status == "Pending"),
            CancelledPurchases = transactions.Count(payment => payment.Status == "Cancelled"),
            FailedPurchases = transactions.Count(payment => payment.Status is "Rejected" or "Failed"),
            OwnerPurchaseCount = ownerTransactions.Count,
            TouristPurchaseCount = touristTransactions.Count,
            OwnerPaidCount = ownerPaid.Count,
            TouristPaidCount = touristPaid.Count,
            OwnerRevenue = ownerPaid.Sum(payment => payment.Amount),
            TouristRevenue = touristPaid.Sum(payment => payment.Amount),
            ActiveOwnerSubscriptions = activeOwnerSubscriptions.Count,
            ActiveTouristSubscriptions = activeTouristSubscriptions.Count,
            RecentTransactions = transactions
                .OrderByDescending(payment => payment.PaidAt ?? payment.CreatedAt)
                .Take(30)
                .ToList()
        };
        model.TotalRevenue = model.OwnerRevenue + model.TouristRevenue;

        model.OwnerPlanRows = BuildPlanRows(ownerTransactions, activeOwnerSubscriptions.Select(subscription => subscription.PaymentPlan).ToList(), "Owner");
        model.TouristPlanRows = BuildPlanRows(touristTransactions, activeTouristSubscriptions.Select(subscription => subscription.PaymentPlan).ToList(), "Tourist");

        model.TopOwnerBuyers = ownerPaid
            .GroupBy(payment => payment.OwnerProfileId)
            .Select(group =>
            {
                var first = group.OrderByDescending(payment => payment.PaidAt ?? payment.CreatedAt).First();
                return new AdminPurchaseBuyerRow
                {
                    OwnerProfileId = group.Key,
                    DisplayName = first.OwnerProfile?.BusinessName ?? first.OwnerProfile?.User?.Username ?? $"Owner #{group.Key}",
                    Contact = first.OwnerProfile?.User?.Email ?? first.OwnerProfile?.User?.Username ?? "-",
                    PaidCount = group.Count(),
                    TotalPaid = group.Sum(payment => payment.Amount),
                    LastPaidAt = group.Max(payment => payment.PaidAt ?? payment.CreatedAt)
                };
            })
            .OrderByDescending(row => row.TotalPaid)
            .ThenByDescending(row => row.PaidCount)
            .Take(10)
            .ToList();

        model.TopTouristBuyers = touristPaid
            .GroupBy(payment => payment.TouristId)
            .Select(group =>
            {
                var first = group.OrderByDescending(payment => payment.PaidAt ?? payment.CreatedAt).First();
                return new AdminPurchaseBuyerRow
                {
                    TouristId = group.Key,
                    DisplayName = first.Tourist?.FullName ?? first.Tourist?.Email ?? $"Du khách #{group.Key}",
                    Contact = first.Tourist?.Email ?? "-",
                    PaidCount = group.Count(),
                    TotalPaid = group.Sum(payment => payment.Amount),
                    LastPaidAt = group.Max(payment => payment.PaidAt ?? payment.CreatedAt)
                };
            })
            .OrderByDescending(row => row.TotalPaid)
            .ThenByDescending(row => row.PaidCount)
            .Take(10)
            .ToList();

        var chartStart = today.AddDays(-13);
        model.Last14Days = Enumerable.Range(0, 14)
            .Select(offset => chartStart.AddDays(offset))
            .Select(date =>
            {
                var paymentsOfDay = paid.Where(payment => (payment.PaidAt ?? payment.CreatedAt).Date == date.Date).ToList();
                return new AdminPurchaseDayRow
                {
                    Date = date,
                    OwnerPurchases = paymentsOfDay.Count(payment => payment.PayerType == "Owner"),
                    TouristPurchases = paymentsOfDay.Count(payment => payment.PayerType == "Tourist"),
                    OwnerRevenue = paymentsOfDay.Where(payment => payment.PayerType == "Owner").Sum(payment => payment.Amount),
                    TouristRevenue = paymentsOfDay.Where(payment => payment.PayerType == "Tourist").Sum(payment => payment.Amount)
                };
            })
            .ToList();

        ViewBag.Status = status;
        ViewBag.PayerType = payerType;
        ViewData["Title"] = "Thống kê lượt mua gói";
        return View(model);
    }

    private static List<AdminPurchasePlanRow> BuildPlanRows(List<PaymentTransaction> payments, List<PaymentPlan?> activePlans, string audience)
    {
        var paid = payments.Where(payment => payment.Status == "Paid").ToList();
        var rows = payments
            .GroupBy(payment => new
            {
                Code = payment.PaymentPlan?.PlanCode ?? payment.Purpose,
                Name = payment.PaymentPlan?.PlanName ?? payment.Purpose,
                Audience = payment.PaymentPlan?.Audience ?? audience
            })
            .Select(group => new AdminPurchasePlanRow
            {
                PlanCode = group.Key.Code,
                PlanName = group.Key.Name,
                Audience = group.Key.Audience,
                PaidCount = group.Count(payment => payment.Status == "Paid"),
                PendingCount = group.Count(payment => payment.Status == "Pending"),
                CancelledCount = group.Count(payment => payment.Status == "Cancelled"),
                Revenue = group.Where(payment => payment.Status == "Paid").Sum(payment => payment.Amount),
                ActiveSubscriptions = activePlans.Count(plan => plan != null && plan.PlanCode == group.Key.Code)
            })
            .ToList();

        foreach (var activeGroup in activePlans.Where(plan => plan != null).GroupBy(plan => new { plan!.PlanCode, plan.PlanName, plan.Audience }))
        {
            if (rows.Any(row => row.PlanCode == activeGroup.Key.PlanCode))
                continue;

            rows.Add(new AdminPurchasePlanRow
            {
                PlanCode = activeGroup.Key.PlanCode,
                PlanName = activeGroup.Key.PlanName,
                Audience = activeGroup.Key.Audience,
                ActiveSubscriptions = activeGroup.Count(),
                PaidCount = paid.Count(payment => payment.PaymentPlan?.PlanCode == activeGroup.Key.PlanCode),
                Revenue = paid.Where(payment => payment.PaymentPlan?.PlanCode == activeGroup.Key.PlanCode).Sum(payment => payment.Amount)
            });
        }

        return rows
            .OrderByDescending(row => row.Revenue)
            .ThenByDescending(row => row.PaidCount)
            .ThenBy(row => row.PlanName)
            .ToList();
    }
}
