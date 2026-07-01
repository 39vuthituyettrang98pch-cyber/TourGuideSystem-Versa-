using UserMobile.Models;

namespace UserMobile.Services;

public interface IPremiumService
{
    Task<TouristPremiumStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TouristPaymentPlan>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TouristPaymentHistory>> GetPaymentsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<TouristCheckoutResult>> CheckoutAsync(
        int planId,
        string paymentMethod,
        CancellationToken cancellationToken = default);
}
