using UserMobile.Models;

namespace UserMobile.Services;

public interface IPoiCatalogService
{
    Task<IReadOnlyList<PlaceItem>> GetAllAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    Task<PlaceItem?> FindByQrAsync(
        string qrData,
        CancellationToken cancellationToken = default);
}
