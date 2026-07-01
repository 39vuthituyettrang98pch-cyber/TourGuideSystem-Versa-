using UserMobile.Models;

namespace UserMobile.Services;

public interface IMenuOrderService
{
    Task<IReadOnlyList<MenuItemDto>> GetPoiMenuAsync(int poiId, CancellationToken cancellationToken = default);
    Task<MenuOrderDto> CreateOrderAsync(CreateMenuOrderRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MenuOrderDto>> GetMyOrdersAsync(CancellationToken cancellationToken = default);
    Task<MenuOrderDto> GetOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<MenuOrderCheckoutResult> CheckoutOrderAsync(int orderId, string paymentMethod, CancellationToken cancellationToken = default);
    Task<string> CancelOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
