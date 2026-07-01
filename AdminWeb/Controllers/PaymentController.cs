using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class PaymentController : Controller
{
    private readonly AppDbContext _context;

    public PaymentController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken)
    {
        var query = _context.PaymentTransactions
            .Include(payment => payment.OwnerProfile)
                .ThenInclude(owner => owner!.User)
            .Include(payment => payment.Tourist)
            .Include(payment => payment.PaymentPlan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(payment => payment.Status == status);

        var paidAmounts = await _context.PaymentTransactions
            .AsNoTracking()
            .Where(payment => payment.Status == "Paid")
            .Select(payment => payment.Amount)
            .ToListAsync(cancellationToken);

        ViewBag.TotalRevenue = paidAmounts.Sum();

        ViewBag.PendingCount = await _context.PaymentTransactions
            .CountAsync(payment => payment.Status == "Pending", cancellationToken);

        var payments = await query
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(payments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
    {
        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payment == null)
            return NotFound();

        if (payment.Status == "Paid")
        {
            TempData["ErrorMessage"] = "Giao dịch này đã được xác nhận trước đó.";
            return RedirectToAction(nameof(Index));
        }

        payment.Status = "Paid";
        payment.PaidAt = DateTime.UtcNow;

        if (payment.PayerType == "Owner" && payment.OwnerProfileId.HasValue && payment.PaymentPlan != null)
        {
            var existingActive = await _context.OwnerSubscriptions
                .Where(item => item.OwnerProfileId == payment.OwnerProfileId.Value && item.Status == "Active")
                .ToListAsync(cancellationToken);

            foreach (var item in existingActive)
                item.Status = "Expired";

            _context.OwnerSubscriptions.Add(new OwnerSubscription
            {
                OwnerProfileId = payment.OwnerProfileId.Value,
                PaymentPlanId = payment.PaymentPlan.Id,
                PaymentTransactionId = payment.Id,
                Status = "Active",
                StartsAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(payment.PaymentPlan.DurationDays <= 0 ? 30 : payment.PaymentPlan.DurationDays)
            });
        }

        if (payment.PayerType == "Tourist" && payment.TouristId.HasValue && payment.PaymentPlan != null)
        {
            var existingActive = await _context.TouristSubscriptions
                .Where(item => item.TouristId == payment.TouristId.Value && item.Status == "Active")
                .ToListAsync(cancellationToken);

            foreach (var item in existingActive)
                item.Status = "Expired";

            _context.TouristSubscriptions.Add(new TouristSubscription
            {
                TouristId = payment.TouristId.Value,
                PaymentPlanId = payment.PaymentPlan.Id,
                PaymentTransactionId = payment.Id,
                Status = "Active",
                StartsAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(payment.PaymentPlan.DurationDays <= 0 ? 30 : payment.PaymentPlan.DurationDays)
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã xác nhận thanh toán và kích hoạt gói.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, CancellationToken cancellationToken)
    {
        var payment = await _context.PaymentTransactions.FindAsync([id], cancellationToken);
        if (payment == null)
            return NotFound();

        payment.Status = "Rejected";
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã từ chối giao dịch.";
        return RedirectToAction(nameof(Index));
    }
}
