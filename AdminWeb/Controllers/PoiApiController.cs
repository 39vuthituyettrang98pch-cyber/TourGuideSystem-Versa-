using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers.Api;

[Route("api/poi")]
[ApiController]
public sealed class PoiApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public PoiApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PoiDto>>>> GetAllPois(
        [FromQuery] string lang = "vi",
        CancellationToken cancellationToken = default)
    {
        lang = NormalizeLanguage(lang);

        var languageNames = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.LanguageCode == "vi" ? 0 : item.LanguageCode == "en" ? 1 : 2)
            .ThenBy(item => item.LanguageName)
            .ToDictionaryAsync(
                item => item.LanguageCode.Trim().ToLower(),
                item => item.LanguageName,
                cancellationToken);

        if (languageNames.Count == 0)
            languageNames["vi"] = "Tiếng Việt";

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Include(poi => poi.PoiCategories)
            .Where(poi => poi.Status == "Approved")
            .OrderByDescending(poi => poi.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var poiIds = pois.Select(poi => poi.Id).ToList();
        var reviews = await _context.PoiReviews
            .AsNoTracking()
            .Include(review => review.Tourist)
            .Where(review => poiIds.Contains(review.PoiId))
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);

        var reviewsByPoi = reviews
            .GroupBy(review => review.PoiId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var data = pois.Select(poi =>
        {
            var selected = poi.Translations.FirstOrDefault(item => item.LanguageCode == lang)
                ?? poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")
                ?? poi.Translations.FirstOrDefault();

            var poiReviews = reviewsByPoi.GetValueOrDefault(poi.Id) ?? [];

            var translationDtos = poi.Translations
                .Where(item => !string.IsNullOrWhiteSpace(item.LanguageCode))
                .GroupBy(item => NormalizeLanguage(item.LanguageCode), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(item => new PoiTranslationDto
                {
                    LanguageCode = NormalizeLanguage(item.LanguageCode),
                    LanguageName = languageNames.GetValueOrDefault(
                        NormalizeLanguage(item.LanguageCode),
                        item.LanguageCode),
                    Name = item.Name,
                    ShortDescription = item.ShortDescription ?? "",
                    FullDescription = item.FullDescription ?? "",
                    AudioUrl = ToAbsoluteUrl(item.AudioUrl),
                    VideoUrl = ToAbsoluteUrl(item.VideoUrl)
                })
                .ToList();

            var sourceTranslation = translationDtos.FirstOrDefault(item => item.LanguageCode == "en")
                ?? translationDtos.FirstOrDefault(item => item.LanguageCode == "vi")
                ?? translationDtos.FirstOrDefault();

            foreach (var language in languageNames)
            {
                if (translationDtos.Any(item => string.Equals(item.LanguageCode, language.Key, StringComparison.OrdinalIgnoreCase)))
                    continue;

                translationDtos.Add(CreateFallbackTranslation(language.Key, language.Value, sourceTranslation, poi.Id));
            }

            return new PoiDto
            {
                Id = poi.Id,
                QrCodeToken = poi.QrCodeToken,
                Name = selected?.Name ?? $"POI #{poi.Id}",
                ShortDescription = selected?.ShortDescription ?? "",
                FullDescription = selected?.FullDescription ?? "",
                CoverImageUrl = ToAbsoluteUrl(poi.CoverImageUrl),
                Latitude = (double)poi.Latitude,
                Longitude = (double)poi.Longitude,
                Radius = poi.Radius,
                AverageRating = poiReviews.Count > 0 ? Math.Round(poiReviews.Average(review => review.Rating), 1) : 0,
                RatingCount = poiReviews.Count,
                RecentReviews = poiReviews.Take(5).Select(ToReviewDto).ToList(),
                CategoryIds = poi.PoiCategories.Select(item => item.CategoryId).ToList(),
                Translations = translationDtos
                    .OrderBy(item => item.LanguageCode == "vi" ? 0 : item.LanguageCode == "en" ? 1 : 2)
                    .ThenBy(item => item.LanguageName)
                    .ToList()
            };
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<PoiDto>>.Ok(data));
    }


    private static PoiTranslationDto CreateFallbackTranslation(
        string languageCode,
        string languageName,
        PoiTranslationDto? source,
        int poiId)
    {
        var name = string.IsNullOrWhiteSpace(source?.Name)
            ? $"POI #{poiId}"
            : source!.Name;

        var description = string.IsNullOrWhiteSpace(source?.FullDescription)
            ? source?.ShortDescription ?? ""
            : source!.FullDescription;

        return new PoiTranslationDto
        {
            LanguageCode = languageCode,
            LanguageName = languageName,
            Name = name,
            ShortDescription = string.IsNullOrWhiteSpace(source?.ShortDescription)
                ? description
                : source!.ShortDescription,
            FullDescription = description,
            AudioUrl = null,
            VideoUrl = null
        };
    }


    private static ReviewDto ToReviewDto(AdminWeb.Models.PoiReview review)
    {
        return new ReviewDto
        {
            Id = review.Id,
            PoiId = review.PoiId,
            TouristId = review.TouristId,
            TouristName = string.IsNullOrWhiteSpace(review.Tourist?.FullName)
                ? $"Du khách #{review.TouristId}"
                : review.Tourist.FullName,
            Rating = review.Rating,
            Comment = review.Comment ?? string.Empty,
            CreatedAt = review.CreatedAt
        };
    }

    private static string NormalizeLanguage(string languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode)
            ? "vi"
            : languageCode.Trim().ToLowerInvariant();
    }

    private string? ToAbsoluteUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{Request.Scheme}://{Request.Host}{Request.PathBase}{normalizedPath}";
    }
}
