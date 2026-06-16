using UserMobile.Models;

namespace UserMobile.Services;

public sealed class PoiCatalogService : IPoiCatalogService
{
    private readonly IApiService _apiService;
    private readonly ILocalizationService _localizationService;
    private IReadOnlyList<PlaceItem>? _cache;
    private string? _cachedLanguageCode;

    public PoiCatalogService(
        IApiService apiService,
        ILocalizationService localizationService)
    {
        _apiService = apiService;
        _localizationService = localizationService;
    }

    public async Task<IReadOnlyList<PlaceItem>> GetAllAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var language = await _localizationService.GetSavedLanguageAsync();
        var languageCode = language?.Code ?? "vi";

        if (!forceRefresh &&
            _cache != null &&
            string.Equals(_cachedLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
        {
            return _cache;
        }

        var response = await _apiService.GetAsync<List<PoiCatalogDto>>(
            $"api/poi?lang={Uri.EscapeDataString(languageCode)}",
            cancellationToken);

        if (!response.Success || response.Data == null)
            throw new InvalidOperationException(response.Message);

        _cache = response.Data.Select(MapPlace).ToList();
        _cachedLanguageCode = languageCode;
        return _cache;
    }

    public async Task<PlaceItem?> FindByQrAsync(
        string qrData,
        CancellationToken cancellationToken = default)
    {
        var normalized = qrData.Trim();
        var places = await GetAllAsync(cancellationToken: cancellationToken);

        return places.FirstOrDefault(place =>
            string.Equals(place.Id, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(place.QrCodeToken, normalized, StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith($"/{place.Id}", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith($"/{place.QrCodeToken}", StringComparison.OrdinalIgnoreCase));
    }

    private static PlaceItem MapPlace(PoiCatalogDto item)
    {
        var narrationLanguages = item.Translations
            .Where(translation => !string.IsNullOrWhiteSpace(translation.LanguageCode))
            .Select(translation => new NarrationLanguage
            {
                Code = translation.LanguageCode,
                Name = translation.LanguageName,
                NativeName = translation.LanguageName,
                AudioUrl = translation.AudioUrl ?? string.Empty,
                VideoUrl = translation.VideoUrl ?? string.Empty
            })
            .ToList();

        return new PlaceItem
        {
            Id = item.Id.ToString(),
            QrCodeToken = item.QrCodeToken,
            Title = item.Name,
            Description = item.ShortDescription,
            Introduction = item.FullDescription,
            ImageUrl = item.CoverImageUrl ?? string.Empty,
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            Radius = item.Radius,
            AverageRating = item.AverageRating,
            RatingCount = item.RatingCount,
            Reviews = item.RecentReviews ?? [],
            HasNarration = narrationLanguages.Any(language =>
                !string.IsNullOrWhiteSpace(language.AudioUrl)),
            NarrationLanguages = narrationLanguages
        };
    }
}
