using UserMobile.Models;

namespace UserMobile.Services;

public sealed class MenuOrderService : IMenuOrderService
{
    private readonly IApiService _apiService;

    public MenuOrderService(IApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<IReadOnlyList<MenuItemDto>> GetPoiMenuAsync(int poiId, CancellationToken cancellationToken = default)
    {
        var response = await _apiService.GetAsync<List<MenuItemDto>>($"api/menu/poi/{poiId}", cancellationToken);
        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data;
    }

    public async Task<MenuOrderDto> CreateOrderAsync(CreateMenuOrderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _apiService.PostAsync<MenuOrderDto>("api/menu/orders", request, cancellationToken);
        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data;
    }

    public async Task<MenuOrderDto> GetOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var response = await _apiService.GetAsync<MenuOrderDto>($"api/menu/orders/{orderId}", cancellationToken);
        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data;
    }

    public async Task<MenuOrderCheckoutResult> CheckoutOrderAsync(int orderId, string paymentMethod, CancellationToken cancellationToken = default)
    {
        var response = await _apiService.PostAsync<MenuOrderCheckoutResult>(
            $"api/menu/orders/{orderId}/checkout",
            new MenuOrderCheckoutRequest { PaymentMethod = paymentMethod },
            cancellationToken);

        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data;
    }

    public async Task<IReadOnlyList<MenuOrderDto>> GetMyOrdersAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiService.GetAsync<List<MenuOrderDto>>("api/menu/orders/my", cancellationToken);
        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data;
    }

    public async Task<string> CancelOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var response = await _apiService.PostAsync<bool>($"api/menu/orders/{orderId}/cancel", new { }, cancellationToken);
        if (!response.Success)
            throw new InvalidOperationException(response.Message);
        return string.IsNullOrWhiteSpace(response.Message) ? "Đã hủy đơn." : response.Message;
    }
}
