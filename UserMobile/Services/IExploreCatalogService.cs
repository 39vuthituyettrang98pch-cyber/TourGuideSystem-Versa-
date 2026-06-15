using UserMobile.Models;

namespace UserMobile.Services;

public interface IExploreCatalogService
{
    Task<IReadOnlyList<CategoryCatalogDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TourCatalogDto>> GetToursAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);
}
