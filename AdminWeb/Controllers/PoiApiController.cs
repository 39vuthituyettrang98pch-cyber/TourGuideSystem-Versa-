using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Services.Payments;
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

        var featuredOwnerIds = await GetFeaturedOwnerIdsAsync(cancellationToken);
        var featuredPoiIds = await GetFeaturedPoiIdsAsync(featuredOwnerIds, cancellationToken);

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Include(poi => poi.PoiCategories)
            .Include(poi => poi.OwnerProfile)
            .Where(poi => poi.Status == "Approved")
            .ToListAsync(cancellationToken);

        pois = pois
            .OrderByDescending(poi => IsFeaturedPoi(poi, featuredOwnerIds, featuredPoiIds))
            .ThenByDescending(poi => poi.CreatedAt)
            .ToList();

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
                IsFeatured = IsFeaturedPoi(poi, featuredOwnerIds, featuredPoiIds),
                OwnerBusinessName = poi.OwnerProfile?.BusinessName,
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


    [HttpGet("by-qr")]
    [HttpGet("by-qr/{qrData}")]
    public async Task<ActionResult<ApiResponse<PoiDto>>> FindByQr(
        [FromRoute] string? qrData,
        [FromQuery] string? qr = null,
        [FromQuery] string lang = "vi",
        CancellationToken cancellationToken = default)
    {
        var normalizedQr = ExtractQrToken(string.IsNullOrWhiteSpace(qr) ? qrData : qr);
        if (string.IsNullOrWhiteSpace(normalizedQr))
            return BadRequest(ApiResponse<PoiDto>.Fail("Mã QR không hợp lệ."));

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

        var featuredOwnerIds = await GetFeaturedOwnerIdsAsync(cancellationToken);
        var featuredPoiIds = await GetFeaturedPoiIdsAsync(featuredOwnerIds, cancellationToken);

        var hasPoiId = int.TryParse(normalizedQr, out var poiId);
        var poi = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.PoiCategories)
            .Include(item => item.OwnerProfile)
            .Where(item => item.Status == "Approved")
            .FirstOrDefaultAsync(item =>
                (hasPoiId && item.Id == poiId) ||
                item.QrCodeToken == normalizedQr,
                cancellationToken);

        if (poi == null)
            return NotFound(ApiResponse<PoiDto>.Fail("Không tìm thấy POI tương ứng với mã QR."));

        var poiReviews = await _context.PoiReviews
            .AsNoTracking()
            .Include(review => review.Tourist)
            .Where(review => review.PoiId == poi.Id)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<PoiDto>.Ok(BuildPoiDto(poi, poiReviews, languageNames, lang, featuredOwnerIds, featuredPoiIds)));
    }

    private PoiDto BuildPoiDto(
        AdminWeb.Models.Poi poi,
        IReadOnlyList<AdminWeb.Models.PoiReview> poiReviews,
        IReadOnlyDictionary<string, string> languageNames,
        string lang,
        IReadOnlySet<int> featuredOwnerIds,
        IReadOnlySet<int> featuredPoiIds)
    {
        var selected = poi.Translations.FirstOrDefault(item => item.LanguageCode == lang)
            ?? poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")
            ?? poi.Translations.FirstOrDefault();

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
            IsFeatured = IsFeaturedPoi(poi, featuredOwnerIds, featuredPoiIds),
            OwnerBusinessName = poi.OwnerProfile?.BusinessName,
            RecentReviews = poiReviews.Take(5).Select(ToReviewDto).ToList(),
            CategoryIds = poi.PoiCategories.Select(item => item.CategoryId).ToList(),
            Translations = translationDtos
                .OrderBy(item => item.LanguageCode == "vi" ? 0 : item.LanguageCode == "en" ? 1 : 2)
                .ThenBy(item => item.LanguageName)
                .ToList()
        };
    }

    private static string ExtractQrToken(string? qrData)
    {
        var normalized = (qrData ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            if (query.TryGetValue("qr", out var qrValues) && !string.IsNullOrWhiteSpace(qrValues.FirstOrDefault()))
                return qrValues.First()!.Trim();

            if (query.TryGetValue("token", out var tokenValues) && !string.IsNullOrWhiteSpace(tokenValues.FirstOrDefault()))
                return tokenValues.First()!.Trim();

            normalized = uri.Segments.LastOrDefault()?.Trim('/') ?? normalized;
        }

        return normalized.Trim().Trim('/');
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

    private async Task<HashSet<int>> GetFeaturedOwnerIdsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var subscriptions = await _context.OwnerSubscriptions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .Where(item =>
                item.Status == "Active" &&
                item.StartsAt <= now &&
                item.ExpiresAt > now &&
                item.PaymentPlan != null &&
                (item.PaymentPlan.Audience == "Owner" || item.PaymentPlan.Audience == "Both"))
            .ToListAsync(cancellationToken);

        var ownerIds = subscriptions
            .Where(item => OwnerFeaturedPlanHelper.IsFeaturedMapPlan(item.PaymentPlan))
            .Select(item => item.OwnerProfileId)
            .ToHashSet();

        var paidTransactions = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .Where(item =>
                item.PayerType == "Owner" &&
                item.OwnerProfileId.HasValue &&
                item.Status == "Paid" &&
                item.PaymentPlan != null &&
                (item.PaymentPlan.Audience == "Owner" || item.PaymentPlan.Audience == "Both"))
            .ToListAsync(cancellationToken);

        foreach (var payment in paidTransactions.Where(payment =>
            payment.OwnerProfileId.HasValue &&
            OwnerFeaturedPlanHelper.IsFeaturedMapPlan(payment.PaymentPlan) &&
            OwnerFeaturedPlanHelper.IsPaymentStillActive(payment)))
        {
            ownerIds.Add(payment.OwnerProfileId!.Value);
        }

        return ownerIds;
    }

    private async Task<HashSet<int>> GetFeaturedPoiIdsAsync(HashSet<int> featuredOwnerIds, CancellationToken cancellationToken)
    {
        if (featuredOwnerIds.Count == 0)
            return [];

        var fromMenuItems = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => featuredOwnerIds.Contains(item.OwnerProfileId) && item.Status != "Hidden")
            .Select(item => item.PoiId)
            .ToListAsync(cancellationToken);

        var fromApprovedOwnerRequests = await _context.PoiOwnerRequests
            .AsNoTracking()
            .Where(item =>
                item.PoiId.HasValue &&
                featuredOwnerIds.Contains(item.OwnerProfileId) &&
                item.Status == "Approved")
            .Select(item => item.PoiId!.Value)
            .ToListAsync(cancellationToken);

        return fromMenuItems
            .Concat(fromApprovedOwnerRequests)
            .ToHashSet();
    }

    private static bool IsFeaturedPoi(
        AdminWeb.Models.Poi poi,
        IReadOnlySet<int> featuredOwnerIds,
        IReadOnlySet<int> featuredPoiIds)
    {
        return (poi.OwnerProfileId.HasValue && featuredOwnerIds.Contains(poi.OwnerProfileId.Value))
            || featuredPoiIds.Contains(poi.Id);
    }
}
