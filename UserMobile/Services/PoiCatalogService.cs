using System.Globalization;
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
        var normalized = ExtractQrToken(qrData);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var languageChanged = await ApplyLanguageFromQrOrDeviceAsync(qrData, cancellationToken);

        var places = await GetAllAsync(forceRefresh: languageChanged, cancellationToken: cancellationToken);
        var localMatch = places.FirstOrDefault(place =>
            string.Equals(place.Id, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(place.QrCodeToken, normalized, StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith($"/{place.Id}", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith($"/{place.QrCodeToken}", StringComparison.OrdinalIgnoreCase));

        if (localMatch != null)
            return localMatch;

        var language = await _localizationService.GetSavedLanguageAsync();
        var languageCode = language?.Code ?? ResolveDeviceLanguageCode();
        var response = await _apiService.GetAsync<PoiCatalogDto>(
            $"api/poi/by-qr?qr={Uri.EscapeDataString(normalized)}&lang={Uri.EscapeDataString(languageCode)}",
            cancellationToken);

        if (!response.Success || response.Data == null)
            return null;

        var resolved = MapPlace(response.Data);
        _cache = _cache is null
            ? new List<PlaceItem> { resolved }
            : _cache.Where(place => !string.Equals(place.Id, resolved.Id, StringComparison.OrdinalIgnoreCase))
                .Append(resolved)
                .ToList();
        _cachedLanguageCode = languageCode;
        return resolved;
    }

    private static string ExtractQrToken(string? qrData)
    {
        var normalized = (qrData ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            var queryToken = TryGetQueryValue(uri.Query, "qr")
                ?? TryGetQueryValue(uri.Query, "token");
            if (!string.IsNullOrWhiteSpace(queryToken))
                return queryToken.Trim();

            return uri.Segments.LastOrDefault()?.Trim('/') ?? normalized;
        }

        return normalized.Trim().Trim('/');
    }

    private async Task<bool> ApplyLanguageFromQrOrDeviceAsync(
        string qrData,
        CancellationToken cancellationToken)
    {
        var requestedCode = ExtractLanguageCode(qrData);
        if (string.IsNullOrWhiteSpace(requestedCode))
            requestedCode = ResolveDeviceLanguageCode();

        if (string.IsNullOrWhiteSpace(requestedCode))
            requestedCode = "vi";

        await _localizationService.RefreshSupportedLanguagesAsync(cancellationToken);
        var language = _localizationService.SupportedLanguages.FirstOrDefault(item =>
            string.Equals(item.Code, requestedCode, StringComparison.OrdinalIgnoreCase));

        if (language is null)
        {
            var shortCode = requestedCode.Split('-', '_')[0];
            language = _localizationService.SupportedLanguages.FirstOrDefault(item =>
                string.Equals(item.Code, shortCode, StringComparison.OrdinalIgnoreCase));
        }

        if (language is null)
            language = _localizationService.SupportedLanguages.FirstOrDefault(item =>
                string.Equals(item.Code, "vi", StringComparison.OrdinalIgnoreCase))
                ?? new LanguageOption { Code = "vi", Name = "Tiếng Việt", NativeName = "Tiếng Việt" };

        var current = (await _localizationService.GetSavedLanguageAsync())?.Code;
        if (string.Equals(current, language.Code, StringComparison.OrdinalIgnoreCase))
            return false;

        await _localizationService.SetLanguageAsync(language);
        _cache = null;
        _cachedLanguageCode = null;
        return true;
    }

    private static string ExtractLanguageCode(string? qrData)
    {
        var raw = (qrData ?? string.Empty).Trim();
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            return string.Empty;

        return NormalizeLanguageCode(
            TryGetQueryValue(uri.Query, "lang")
            ?? TryGetQueryValue(uri.Query, "language")
            ?? TryGetQueryValue(uri.Query, "locale"));
    }

    private static string ResolveDeviceLanguageCode()
    {
        var code = CultureInfo.CurrentUICulture.Name;
        var normalized = NormalizeLanguageCode(code);
        if (!string.IsNullOrWhiteSpace(normalized))
            return normalized;

        return NormalizeLanguageCode(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    private static string NormalizeLanguageCode(string? value)
    {
        var code = (value ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        if (code.Length > 10)
            code = code[..10];

        return code.All(character => char.IsLetterOrDigit(character) || character == '-')
            ? code
            : string.Empty;
    }

    private static string? TryGetQueryValue(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var name = Uri.UnescapeDataString(parts[0]);
            if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                continue;

            return parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return null;
    }

    private PlaceItem MapPlace(PoiCatalogDto item)
    {
        var narrationLanguages = item.Translations
            .Where(translation => !string.IsNullOrWhiteSpace(translation.LanguageCode))
            .Select(translation => new NarrationLanguage
            {
                Code = translation.LanguageCode,
                Name = translation.LanguageName,
                NativeName = translation.LanguageName,
                AudioUrl = ResolveMediaUrl(translation.AudioUrl),
                VideoUrl = ResolveMediaUrl(translation.VideoUrl)
            })
            .ToList();

        return new PlaceItem
        {
            Id = item.Id.ToString(),
            QrCodeToken = item.QrCodeToken,
            Title = item.Name,
            Description = item.ShortDescription,
            Introduction = item.FullDescription,
            ImageUrl = ResolveMediaUrl(item.CoverImageUrl),
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            Radius = item.Radius,
            AverageRating = item.AverageRating,
            RatingCount = item.RatingCount,
            IsFeatured = item.IsFeatured,
            OwnerBusinessName = item.OwnerBusinessName ?? string.Empty,
            Reviews = item.RecentReviews ?? [],
            HasNarration = narrationLanguages.Any(language =>
                !string.IsNullOrWhiteSpace(language.AudioUrl)),
            NarrationLanguages = narrationLanguages
        };
    }

    private string ResolveMediaUrl(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var apiBase = _apiService.BaseAddress;

        if (Uri.TryCreate(raw, UriKind.Absolute, out var absoluteUri))
        {
            if (apiBase != null && IsLoopbackHost(absoluteUri.Host) && !IsLoopbackHost(apiBase.Host))
            {
                var builder = new UriBuilder(absoluteUri)
                {
                    Scheme = apiBase.Scheme,
                    Host = apiBase.Host,
                    Port = apiBase.Port
                };
                return builder.Uri.ToString();
            }

            return absoluteUri.ToString();
        }

        if (apiBase == null)
            return raw;

        return new Uri(apiBase, raw.TrimStart('/')).ToString();
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
}
