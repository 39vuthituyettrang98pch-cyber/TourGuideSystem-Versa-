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
            .ToDictionaryAsync(
                item => item.LanguageCode,
                item => item.LanguageName,
                cancellationToken);
        languageNames["vi"] = "Tiếng Việt";

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Include(poi => poi.PoiCategories)
            .Where(poi => poi.Status == "Approved")
            .OrderByDescending(poi => poi.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var data = pois.Select(poi =>
        {
            var selected = poi.Translations.FirstOrDefault(item => item.LanguageCode == lang)
                ?? poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")
                ?? poi.Translations.FirstOrDefault();

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
                CategoryIds = poi.PoiCategories.Select(item => item.CategoryId).ToList(),
                Translations = poi.Translations
                    .OrderBy(item => item.LanguageCode)
                    .Select(item => new PoiTranslationDto
                    {
                        LanguageCode = item.LanguageCode,
                        LanguageName = languageNames.GetValueOrDefault(
                            item.LanguageCode,
                            item.LanguageCode),
                        Name = item.Name,
                        ShortDescription = item.ShortDescription ?? "",
                        FullDescription = item.FullDescription ?? "",
                        AudioUrl = ToAbsoluteUrl(item.AudioUrl),
                        VideoUrl = ToAbsoluteUrl(item.VideoUrl)
                    })
                    .ToList()
            };
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<PoiDto>>.Ok(data));
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
