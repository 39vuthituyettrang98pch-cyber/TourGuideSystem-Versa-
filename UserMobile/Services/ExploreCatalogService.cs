using UserMobile.Models;

namespace UserMobile.Services;

public sealed class ExploreCatalogService : IExploreCatalogService
{
    private readonly IApiService _apiService;
    private readonly ILocalizationService _localizationService;

    public ExploreCatalogService(
        IApiService apiService,
        ILocalizationService localizationService)
    {
        _apiService = apiService;
        _localizationService = localizationService;
    }

    public async Task<IReadOnlyList<CategoryCatalogDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var languageCode = await GetLanguageCodeAsync();
        var response = await _apiService.GetAsync<List<CategoryCatalogDto>>(
            $"api/category?lang={Uri.EscapeDataString(languageCode)}",
            cancellationToken);

        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data;
    }

    public async Task<IReadOnlyList<TourCatalogDto>> GetToursAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var languageCode = await GetLanguageCodeAsync();
        var route = $"api/tour?lang={Uri.EscapeDataString(languageCode)}";
        if (categoryId.HasValue)
            route += $"&categoryId={categoryId.Value}";

        var response = await _apiService.GetAsync<List<TourCatalogDto>>(route, cancellationToken);
        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        return response.Data;
    }

    private async Task<string> GetLanguageCodeAsync()
    {
        return (await _localizationService.GetSavedLanguageAsync())?.Code ?? "vi";
    }
}
