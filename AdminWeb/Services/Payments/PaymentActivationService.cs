using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Services.Payments;

public sealed class PaymentActivationService
{
    private readonly AppDbContext _context;

    public PaymentActivationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task MarkPaymentPaidAsync(PaymentTransaction payment, string gatewayStatus, CancellationToken cancellationToken)
    {
        if (payment.Status == "Paid")
            return;

        payment.Status = "Paid";
        payment.GatewayStatus = string.IsNullOrWhiteSpace(gatewayStatus) ? "PAID" : gatewayStatus;
        payment.PaidAt = DateTime.UtcNow;

        if (payment.PaymentPlanId.HasValue && payment.PaymentPlan != null)
        {
            if (payment.PayerType == "Owner" && payment.OwnerProfileId.HasValue)
                await ActivateOwnerSubscriptionAsync(payment, cancellationToken);
            else if (payment.PayerType == "Tourist" && payment.TouristId.HasValue)
                await ActivateTouristSubscriptionAsync(payment, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ActivateOwnerSubscriptionAsync(PaymentTransaction payment, CancellationToken cancellationToken)
    {
        var exists = await _context.OwnerSubscriptions.AnyAsync(item => item.PaymentTransactionId == payment.Id, cancellationToken);
        if (exists || payment.OwnerProfileId == null || payment.PaymentPlan == null || payment.PaymentPlanId == null)
            return;

        var latestActive = await _context.OwnerSubscriptions
            .Where(item => item.OwnerProfileId == payment.OwnerProfileId.Value && item.Status == "Active")
            .OrderByDescending(item => item.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        var startsAt = latestActive != null && latestActive.ExpiresAt > DateTime.UtcNow
            ? latestActive.ExpiresAt
            : DateTime.UtcNow;

        _context.OwnerSubscriptions.Add(new OwnerSubscription
        {
            OwnerProfileId = payment.OwnerProfileId.Value,
            PaymentPlanId = payment.PaymentPlanId.Value,
            PaymentTransactionId = payment.Id,
            Status = "Active",
            StartsAt = startsAt,
            ExpiresAt = startsAt.AddDays(payment.PaymentPlan.DurationDays <= 0 ? 30 : payment.PaymentPlan.DurationDays)
        });
    }

    private async Task ActivateTouristSubscriptionAsync(PaymentTransaction payment, CancellationToken cancellationToken)
    {
        var exists = await _context.TouristSubscriptions.AnyAsync(item => item.PaymentTransactionId == payment.Id, cancellationToken);
        if (exists || payment.TouristId == null || payment.PaymentPlan == null || payment.PaymentPlanId == null)
            return;

        var latestActive = await _context.TouristSubscriptions
            .Where(item => item.TouristId == payment.TouristId.Value && item.Status == "Active")
            .OrderByDescending(item => item.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        var startsAt = latestActive != null && latestActive.ExpiresAt > DateTime.UtcNow
            ? latestActive.ExpiresAt
            : DateTime.UtcNow;

        _context.TouristSubscriptions.Add(new TouristSubscription
        {
            TouristId = payment.TouristId.Value,
            PaymentPlanId = payment.PaymentPlanId.Value,
            PaymentTransactionId = payment.Id,
            Status = "Active",
            StartsAt = startsAt,
            ExpiresAt = startsAt.AddDays(payment.PaymentPlan.DurationDays <= 0 ? 30 : payment.PaymentPlan.DurationDays)
        });
    }
}
