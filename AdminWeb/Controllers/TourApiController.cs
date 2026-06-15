using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers.Api;

[Route("api/tour")]
[ApiController]
public sealed class TourApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public TourApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TourDto>>>> GetTours(
        [FromQuery] string lang = "vi",
        [FromQuery] string? keyword = null,
        [FromQuery] int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        lang = NormalizeLanguage(lang);
        var query = _context.Tours
            .AsNoTracking()
            .Include(tour => tour.Translations)
            .Include(tour => tour.TourPois)
                .ThenInclude(item => item.Poi)
                    .ThenInclude(poi => poi!.Translations)
            .Include(tour => tour.TourPois)
                .ThenInclude(item => item.Poi)
                    .ThenInclude(poi => poi!.PoiCategories)
            .AsSplitQuery()
            .Where(tour => tour.Status == "active");

        if (categoryId.HasValue)
        {
            query = query.Where(tour =>
                tour.TourPois.Any(item =>
                    item.Poi!.PoiCategories.Any(category => category.CategoryId == categoryId.Value)));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(tour =>
                tour.Translations.Any(translation => translation.Title.Contains(keyword)));
        }

        var tours = await query.OrderBy(tour => tour.Id).ToListAsync(cancellationToken);
        var data = tours.Select(tour =>
        {
            var translation = SelectTranslation(tour.Translations, lang);
            return new TourDto
            {
                Id = tour.Id,
                Title = translation?.Title ?? $"Tour #{tour.Id}",
                Description = translation?.Description ?? "",
                DurationMinutes = tour.EstimatedTime,
                Pois = tour.TourPois
                    .Where(item => item.Poi?.Status == "Approved")
                    .OrderBy(item => item.SequenceOrder)
                    .Select(item => MapPoi(item.Poi!, item.SequenceOrder, lang))
                    .ToList()
            };
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<TourDto>>.Ok(data));
    }

    [HttpGet("{tourId:int}/pois")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TourPoiDto>>>> GetTourPois(
        int tourId,
        [FromQuery] string lang = "vi",
        CancellationToken cancellationToken = default)
    {
        lang = NormalizeLanguage(lang);
        var tour = await _context.Tours
            .AsNoTracking()
            .Include(item => item.TourPois)
                .ThenInclude(item => item.Poi)
                    .ThenInclude(poi => poi!.Translations)
            .FirstOrDefaultAsync(item => item.Id == tourId && item.Status == "active", cancellationToken);

        if (tour == null)
            return NotFound(ApiResponse<IReadOnlyList<TourPoiDto>>.Fail("Tour không tồn tại."));

        var data = tour.TourPois
            .Where(item => item.Poi?.Status == "Approved")
            .OrderBy(item => item.SequenceOrder)
            .Select(item => MapPoi(item.Poi!, item.SequenceOrder, lang))
            .ToList();
        return Ok(ApiResponse<IReadOnlyList<TourPoiDto>>.Ok(data));
    }

    private TourPoiDto MapPoi(AdminWeb.Models.Poi poi, int sequenceOrder, string languageCode)
    {
        var translation = SelectTranslation(poi.Translations, languageCode);
        return new TourPoiDto
        {
            Id = poi.Id,
            SequenceOrder = sequenceOrder,
            Name = translation?.Name ?? $"POI #{poi.Id}",
            ShortDescription = translation?.ShortDescription ?? "",
            FullDescription = translation?.FullDescription ?? "",
            AudioUrl = ToAbsoluteUrl(translation?.AudioUrl),
            VideoUrl = ToAbsoluteUrl(translation?.VideoUrl),
            CoverImageUrl = ToAbsoluteUrl(poi.CoverImageUrl),
            Latitude = (double)poi.Latitude,
            Longitude = (double)poi.Longitude,
            Radius = poi.Radius
        };
    }

    private static AdminWeb.Models.TourTranslation? SelectTranslation(
        IEnumerable<AdminWeb.Models.TourTranslation> translations,
        string languageCode)
    {
        return translations.FirstOrDefault(item => item.LanguageCode == languageCode)
            ?? translations.FirstOrDefault(item => item.LanguageCode == "vi")
            ?? translations.FirstOrDefault();
    }

    private static AdminWeb.Models.PoiTranslation? SelectTranslation(
        IEnumerable<AdminWeb.Models.PoiTranslation> translations,
        string languageCode)
    {
        return translations.FirstOrDefault(item => item.LanguageCode == languageCode)
            ?? translations.FirstOrDefault(item => item.LanguageCode == "vi")
            ?? translations.FirstOrDefault();
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

        var normalized = path.StartsWith('/') ? path : $"/{path}";
        return $"{Request.Scheme}://{Request.Host}{Request.PathBase}{normalized}";
    }
}
