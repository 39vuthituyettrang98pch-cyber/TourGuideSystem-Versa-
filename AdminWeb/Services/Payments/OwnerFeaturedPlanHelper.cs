using AdminWeb.Models;

namespace AdminWeb.Services.Payments;

public static class OwnerFeaturedPlanHelper
{
    public static bool IsFeaturedMapPlan(PaymentPlan? plan)
    {
        if (plan == null)
            return false;

        var text = $"{plan.PlanCode} {plan.PlanName} {plan.Description}".ToUpperInvariant();

        return text.Contains("FEATURED", StringComparison.OrdinalIgnoreCase)
            || text.Contains("NOI_BAT", StringComparison.OrdinalIgnoreCase)
            || text.Contains("NỔI BẬT", StringComparison.OrdinalIgnoreCase)
            || text.Contains("NOI BAT", StringComparison.OrdinalIgnoreCase)
            || text.Contains("PREMIUM", StringComparison.OrdinalIgnoreCase)
            || text.Contains("PRO", StringComparison.OrdinalIgnoreCase)
            || text.Contains("PRIORITY", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ƯU TIÊN", StringComparison.OrdinalIgnoreCase)
            || text.Contains("UU TIEN", StringComparison.OrdinalIgnoreCase)
            || plan.Price >= 199000m;
    }


    public static bool IsPaymentStillActive(PaymentTransaction payment)
    {
        var plan = payment.PaymentPlan;
        if (plan == null)
            return false;

        var paidAt = payment.PaidAt ?? payment.CreatedAt;
        var durationDays = plan.DurationDays <= 0 ? 30 : plan.DurationDays;
        return paidAt.AddDays(durationDays) > DateTime.UtcNow;
    }

    public static string FeaturedBadgeText => "Nổi bật";
}
