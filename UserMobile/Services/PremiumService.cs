using UserMobile.Models;

namespace UserMobile.Services;

public sealed class PremiumService : IPremiumService
{
    private readonly IApiService _apiService;

    public PremiumService(IApiService apiService) => _apiService = apiService;

    public async Task<TouristPremiumStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiService.GetAsync<TouristPremiumStatus>("api/tourist/premium", cancellationToken);
        return Read(response);
    }

    public async Task<IReadOnlyList<TouristPaymentPlan>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiService.GetAsync<List<TouristPaymentPlan>>("api/tourist/plans", cancellationToken);
        return Read(response);
    }

    public async Task<IReadOnlyList<TouristPaymentHistory>> GetPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiService.GetAsync<List<TouristPaymentHistory>>("api/tourist/payments", cancellationToken);
        return Read(response);
    }

    public Task<ApiResponse<TouristCheckoutResult>> CheckoutAsync(
        int planId,
        string paymentMethod,
        CancellationToken cancellationToken = default) =>
        _apiService.PostAsync<TouristCheckoutResult>("api/tourist/payments/checkout", new
        {
            PlanId = planId,
            PaymentMethod = paymentMethod
        }, cancellationToken);

    private static T Read<T>(ApiResponse<T> response)
    {
        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);
        return response.Data;
    }
}
