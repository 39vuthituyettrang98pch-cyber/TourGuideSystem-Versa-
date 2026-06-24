using AdminWeb.Areas.DuKhach.Models;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using AdminWeb.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
public sealed class MapController : Controller
{
    private const int PointsPerPoi = 10;
    private const string FallbackLanguageCode = "vi";

    private readonly AppDbContext _context;
    private readonly TouristAudioQuotaService _audioQuota;

    public MapController(AppDbContext context, TouristAudioQuotaService audioQuota)
    {
        _context = context;
        _audioQuota = audioQuota;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var activeLanguages = await GetActiveLanguagesAsync(cancellationToken);
        var selectedLanguageCode = GetSelectedLanguageCode(activeLanguages.Keys);
        var selectedLanguageName = activeLanguages.TryGetValue(selectedLanguageCode, out var languageName)
            ? languageName
            : activeLanguages.GetValueOrDefault("vi", "Tiếng Việt");

        ViewBag.SelectedLanguageCode = selectedLanguageCode;
        ViewBag.SelectedLanguageName = selectedLanguageName;

        var touristId = TryGetTouristId();
        var discoveredIds = touristId.HasValue
            ? await _context.TouristPoiDiscoveries
                .AsNoTracking()
                .Where(item => item.TouristId == touristId.Value)
                .Select(item => item.PoiId)
                .ToListAsync(cancellationToken)
            : new List<int>();

        var discoveredSet = discoveredIds.ToHashSet();

        var favoriteIds = touristId.HasValue
            ? await _context.TouristFavorites
                .AsNoTracking()
                .Where(item => item.TouristId == touristId.Value && item.TargetType == "POI")
                .Select(item => item.TargetId)
                .ToListAsync(cancellationToken)
            : new List<int>();
        var bookmarkedSet = favoriteIds.ToHashSet();

        var hasPremiumActive = touristId.HasValue
            && await HasActivePremiumAsync(touristId.Value, cancellationToken);
        var audioQuota = touristId.HasValue
            ? await _audioQuota.GetStatusAsync(touristId.Value, cancellationToken)
            : null;

        var featuredOwnerIds = await GetFeaturedOwnerIdsAsync(cancellationToken);
        var featuredPoiIds = await GetFeaturedPoiIdsAsync(featuredOwnerIds, cancellationToken);

        var allReviews = await _context.Set<PoiReview>()
            .AsNoTracking()
            .Include(review => review.Tourist)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);

        var reviewsByPoi = allReviews
            .GroupBy(review => review.PoiId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.OwnerProfile)
            .Where(item =>
                item.Status == "Approved" &&
                item.Latitude >= -90 && item.Latitude <= 90 &&
                item.Longitude >= -180 && item.Longitude <= 180)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var model = new DuKhachMapViewModel
        {
            IsTouristSignedIn = touristId.HasValue,
            HasPremiumActive = hasPremiumActive,
            AudioDailyLimit = TouristAudioQuotaService.FreeDailyLimit,
            AudioPlaysUsedToday = audioQuota?.UsedToday ?? 0,
            AudioPlaysRemainingToday = audioQuota?.RemainingToday,
            DiscoveredCount = discoveredSet.Count,
            TotalPoiCount = pois.Count,
            FeaturedPoiCount = pois.Count(item => IsFeaturedPoi(item, featuredOwnerIds, featuredPoiIds)),
            SelectedLanguageCode = selectedLanguageCode,
            SelectedLanguageName = selectedLanguageName,
            Languages = activeLanguages
                .Select(item => new DuKhachLanguageOptionViewModel
                {
                    Code = item.Key,
                    Name = item.Value
                })
                .OrderBy(item => item.Code == "vi" ? 0 : item.Code == "en" ? 1 : 2)
                .ThenBy(item => item.Name)
                .ToList(),
            Pois = pois.Select(item =>
            {
                var narrations = item.Translations
                    .Where(translation => !string.IsNullOrWhiteSpace(translation.LanguageCode))
                    .OrderBy(translation => translation.LanguageCode == selectedLanguageCode ? 0 : translation.LanguageCode == "vi" ? 1 : 2)
                    .ThenBy(translation => translation.LanguageCode)
                    .Select(translation => new DuKhachPoiNarrationViewModel
                    {
                        LanguageCode = NormalizeLanguageCode(translation.LanguageCode),
                        LanguageName = activeLanguages.GetValueOrDefault(NormalizeLanguageCode(translation.LanguageCode), translation.LanguageCode.ToUpperInvariant()),
                        Name = translation.Name,
                        ShortDescription = translation.ShortDescription ?? "",
                        FullDescription = translation.FullDescription ?? "",
                        NarrationText = !string.IsNullOrWhiteSpace(translation.TtsScript)
                            ? translation.TtsScript!
                            : translation.FullDescription ?? translation.ShortDescription ?? translation.Name,
                        AudioUrl = ToAbsoluteUrl(translation.AudioUrl),
                        VideoUrl = ToAbsoluteUrl(translation.VideoUrl)
                    })
                    .GroupBy(item => item.LanguageCode, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();

                narrations = EnsureNarrationsForAllActiveLanguages(narrations, activeLanguages);

                var selectedNarration = SelectNarration(narrations, selectedLanguageCode);

                return new DuKhachPoiMapItemViewModel
                {
                    Id = item.Id,
                    Name = selectedNarration?.Name ?? $"POI #{item.Id}",
                    ShortDescription = selectedNarration?.ShortDescription ?? "",
                    FullDescription = selectedNarration?.FullDescription ?? "",
                    NarrationText = selectedNarration?.NarrationText ?? "",
                    LanguageCode = selectedNarration?.LanguageCode ?? selectedLanguageCode,
                    LanguageName = selectedNarration?.LanguageName ?? selectedLanguageName,
                    CoverImageUrl = ToAbsoluteUrl(item.CoverImageUrl),
                    AudioUrl = selectedNarration?.AudioUrl,
                    VideoUrl = selectedNarration?.VideoUrl,
                    Latitude = (double)item.Latitude,
                    Longitude = (double)item.Longitude,
                    Radius = item.Radius,
                    IsDiscovered = discoveredSet.Contains(item.Id),
                    IsFeatured = IsFeaturedPoi(item, featuredOwnerIds, featuredPoiIds),
                    OwnerBusinessName = item.OwnerProfile?.BusinessName,
                    IsBookmarked = bookmarkedSet.Contains(item.Id),
                    AverageRating = reviewsByPoi.TryGetValue(item.Id, out var ratingReviews) && ratingReviews.Count > 0
                        ? ratingReviews.Average(review => review.Rating)
                        : 0,
                    RatingCount = reviewsByPoi.TryGetValue(item.Id, out var countReviews) ? countReviews.Count : 0,
                    Narrations = narrations,
                    Reviews = reviewsByPoi.TryGetValue(item.Id, out var poiReviews)
                        ? poiReviews.Select(review => new DuKhachReviewViewModel
                        {
                            TouristName = string.IsNullOrWhiteSpace(review.Tourist?.FullName)
                                ? $"Du khách #{review.TouristId}"
                                : review.Tourist.FullName,
                            Rating = review.Rating,
                            Comment = review.Comment,
                            CreatedAt = review.CreatedAt
                        }).ToList()
                        : new List<DuKhachReviewViewModel>()
                };
            })
            .OrderByDescending(item => item.IsFeatured)
            .ThenByDescending(item => item.AverageRating)
            .ThenBy(item => item.Name)
            .ToList()
        };

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Details(int id, string? lang = null, CancellationToken cancellationToken = default)
    {
        var activeLanguages = await GetActiveLanguagesAsync(cancellationToken);
        var selectedLanguageCode = GetSelectedLanguageCode(activeLanguages.Keys);
        if (!string.IsNullOrWhiteSpace(lang))
        {
            var normalizedLang = NormalizeLanguageCode(lang);
            if (activeLanguages.ContainsKey(normalizedLang))
                selectedLanguageCode = normalizedLang;
        }

        var selectedLanguageName = activeLanguages.TryGetValue(selectedLanguageCode, out var languageName)
            ? languageName
            : activeLanguages.GetValueOrDefault("vi", "Tiếng Việt");

        ViewBag.SelectedLanguageCode = selectedLanguageCode;
        ViewBag.SelectedLanguageName = selectedLanguageName;

        var touristId = TryGetTouristId();
        var isDiscovered = touristId.HasValue && await _context.TouristPoiDiscoveries
            .AsNoTracking()
            .AnyAsync(item => item.TouristId == touristId.Value && item.PoiId == id, cancellationToken);
        var isBookmarked = touristId.HasValue && await _context.TouristFavorites
            .AsNoTracking()
            .AnyAsync(item => item.TouristId == touristId.Value && item.TargetType == "POI" && item.TargetId == id, cancellationToken);
        var hasPremiumActive = touristId.HasValue
            && await HasActivePremiumAsync(touristId.Value, cancellationToken);
        var audioQuota = touristId.HasValue
            ? await _audioQuota.GetStatusAsync(touristId.Value, cancellationToken)
            : null;

        var featuredOwnerIds = await GetFeaturedOwnerIdsAsync(cancellationToken);
        var featuredPoiIds = await GetFeaturedPoiIdsAsync(featuredOwnerIds, cancellationToken);

        var poi = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.OwnerProfile)
            .FirstOrDefaultAsync(item =>
                item.Id == id &&
                item.Status == "Approved" &&
                item.Latitude >= -90 && item.Latitude <= 90 &&
                item.Longitude >= -180 && item.Longitude <= 180,
                cancellationToken);

        if (poi == null)
            return NotFound();

        var poiReviews = await _context.Set<PoiReview>()
            .AsNoTracking()
            .Include(review => review.Tourist)
            .Where(review => review.PoiId == id)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);

        var narrations = poi.Translations
            .Where(translation => !string.IsNullOrWhiteSpace(translation.LanguageCode))
            .OrderBy(translation => translation.LanguageCode == selectedLanguageCode ? 0 : translation.LanguageCode == "vi" ? 1 : 2)
            .ThenBy(translation => translation.LanguageCode)
            .Select(translation => new DuKhachPoiNarrationViewModel
            {
                LanguageCode = NormalizeLanguageCode(translation.LanguageCode),
                LanguageName = activeLanguages.GetValueOrDefault(NormalizeLanguageCode(translation.LanguageCode), translation.LanguageCode.ToUpperInvariant()),
                Name = translation.Name,
                ShortDescription = translation.ShortDescription ?? "",
                FullDescription = translation.FullDescription ?? "",
                NarrationText = !string.IsNullOrWhiteSpace(translation.TtsScript)
                    ? translation.TtsScript!
                    : translation.FullDescription ?? translation.ShortDescription ?? translation.Name,
                AudioUrl = ToAbsoluteUrl(translation.AudioUrl),
                VideoUrl = ToAbsoluteUrl(translation.VideoUrl)
            })
            .GroupBy(item => item.LanguageCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        narrations = EnsureNarrationsForAllActiveLanguages(narrations, activeLanguages);
        var selectedNarration = SelectNarration(narrations, selectedLanguageCode);

        var model = new DuKhachPoiDetailsPageViewModel
        {
            IsTouristSignedIn = touristId.HasValue,
            HasPremiumActive = hasPremiumActive,
            AudioDailyLimit = TouristAudioQuotaService.FreeDailyLimit,
            AudioPlaysUsedToday = audioQuota?.UsedToday ?? 0,
            AudioPlaysRemainingToday = audioQuota?.RemainingToday,
            SelectedLanguageCode = selectedLanguageCode,
            SelectedLanguageName = selectedLanguageName,
            Languages = activeLanguages
                .Select(item => new DuKhachLanguageOptionViewModel { Code = item.Key, Name = item.Value })
                .OrderBy(item => item.Code == "vi" ? 0 : item.Code == "en" ? 1 : 2)
                .ThenBy(item => item.Name)
                .ToList(),
            Poi = new DuKhachPoiMapItemViewModel
            {
                Id = poi.Id,
                Name = selectedNarration?.Name ?? $"POI #{poi.Id}",
                ShortDescription = selectedNarration?.ShortDescription ?? "",
                FullDescription = selectedNarration?.FullDescription ?? "",
                NarrationText = selectedNarration?.NarrationText ?? "",
                LanguageCode = selectedNarration?.LanguageCode ?? selectedLanguageCode,
                LanguageName = selectedNarration?.LanguageName ?? selectedLanguageName,
                CoverImageUrl = ToAbsoluteUrl(poi.CoverImageUrl),
                AudioUrl = selectedNarration?.AudioUrl,
                VideoUrl = selectedNarration?.VideoUrl,
                Latitude = (double)poi.Latitude,
                Longitude = (double)poi.Longitude,
                Radius = poi.Radius,
                IsDiscovered = isDiscovered,
                IsFeatured = IsFeaturedPoi(poi, featuredOwnerIds, featuredPoiIds),
                OwnerBusinessName = poi.OwnerProfile?.BusinessName,
                IsBookmarked = isBookmarked,
                AverageRating = poiReviews.Count > 0 ? poiReviews.Average(review => review.Rating) : 0,
                RatingCount = poiReviews.Count,
                Narrations = narrations,
                Reviews = poiReviews.Select(review => new DuKhachReviewViewModel
                {
                    TouristName = string.IsNullOrWhiteSpace(review.Tourist?.FullName)
                        ? $"Du khách #{review.TouristId}"
                        : review.Tourist.FullName,
                    Rating = review.Rating,
                    Comment = review.Comment,
                    CreatedAt = review.CreatedAt
                }).ToList()
            }
        };

        return View(model);
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn([FromBody] DuKhachCheckInRequest request, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var languageCode = NormalizeLanguageCode(request.LanguageCode);
        var poi = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .FirstOrDefaultAsync(item => item.Id == request.PoiId && item.Status == "Approved", cancellationToken);

        if (poi == null)
            return BadRequest(new { success = false, message = "POI không tồn tại hoặc chưa được duyệt." });

        if (!request.Latitude.HasValue || !request.Longitude.HasValue)
            return BadRequest(new { success = false, message = "Vui lòng bật GPS và cấp quyền vị trí để check-in." });

        if (request.Latitude.Value is < -90 or > 90 || request.Longitude.Value is < -180 or > 180)
            return BadRequest(new { success = false, message = "Tọa độ GPS không hợp lệ." });

        var distance = CalculateDistanceMeters(
            request.Latitude.Value,
            request.Longitude.Value,
            (double)poi.Latitude,
            (double)poi.Longitude);
        var accuracyAllowance = Math.Clamp(request.AccuracyMeters ?? 0, 0, 100);
        var allowedDistance = Math.Max(poi.Radius, 25) + accuracyAllowance;

        if (distance > allowedDistance)
        {
            return BadRequest(new
            {
                success = false,
                message = $"Bạn chưa ở gần địa điểm này nên không thể check-in. Hiện còn cách khoảng {Math.Round(distance)} m."
            });
        }

        var existing = await _context.TouristPoiDiscoveries
            .AnyAsync(item => item.TouristId == touristId && item.PoiId == poi.Id, cancellationToken);

        var poiName = poi.Translations.FirstOrDefault(translation => translation.LanguageCode == languageCode)?.Name
            ?? poi.Translations.FirstOrDefault(translation => translation.LanguageCode == "vi")?.Name
            ?? poi.Translations.FirstOrDefault()?.Name
            ?? $"POI #{poi.Id}";

        if (existing)
        {
            return Ok(new
            {
                success = true,
                isNewDiscovery = false,
                message = $"Bạn đã check-in tại {poiName} rồi."
            });
        }

        _context.TouristPoiDiscoveries.Add(new TouristPoiDiscovery
        {
            TouristId = touristId,
            PoiId = poi.Id,
            DiscoveryMethod = "GPS",
            PointsAwarded = PointsPerPoi,
            VisitorLatitude = (decimal)request.Latitude.Value,
            VisitorLongitude = (decimal)request.Longitude.Value,
            DiscoveredAt = DateTime.UtcNow
        });

        _context.VisitorPlaybackLogs.Add(new VisitorPlaybackLog
        {
            TouristId = touristId,
            DeviceId = $"web-tourist-{touristId}",
            PoiId = poi.Id,
            LanguageCode = languageCode,
            TriggerType = "WebMapCheckIn",
            VisitorLatitude = (decimal)request.Latitude.Value,
            VisitorLongitude = (decimal)request.Longitude.Value,
            ListenDuration = 0,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            isNewDiscovery = true,
            pointsAwarded = PointsPerPoi,
            message = $"Check-in thành công tại {poiName}. +{PointsPerPoi} điểm khám phá!"
        });
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBookmark([FromBody] int poiId, CancellationToken cancellationToken)
    {
        var poiExists = await _context.Pois
            .AnyAsync(item => item.Id == poiId && item.Status == "Approved", cancellationToken);
        if (!poiExists)
            return BadRequest(new { success = false, message = "POI không tồn tại hoặc chưa được duyệt." });

        var touristId = GetTouristId();
        var existing = await _context.TouristFavorites.FirstOrDefaultAsync(
            item => item.TouristId == touristId && item.TargetType == "POI" && item.TargetId == poiId,
            cancellationToken);

        bool isBookmarked;
        if (existing != null)
        {
            _context.TouristFavorites.Remove(existing);
            isBookmarked = false;
        }
        else
        {
            _context.TouristFavorites.Add(new TouristFavorite
            {
                TouristId = touristId,
                TargetType = "POI",
                TargetId = poiId,
                CreatedAt = DateTime.UtcNow
            });
            isBookmarked = true;
        }

        var legacyBookmarks = await _context.TouristBookmarks
            .Where(item => item.TouristId == touristId && item.PoiId == poiId)
            .ToListAsync(cancellationToken);
        if (legacyBookmarks.Count > 0)
            _context.TouristBookmarks.RemoveRange(legacyBookmarks);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, isBookmarked });
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview([FromBody] DuKhachSubmitReviewRequest request, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();

        var poiExists = await _context.Pois
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.PoiId && item.Status == "Approved", cancellationToken);

        if (!poiExists)
            return BadRequest(new { success = false, message = "POI không tồn tại hoặc chưa được duyệt." });

        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest(new { success = false, message = "Vui lòng chọn số sao từ 1 đến 5." });

        var comment = string.IsNullOrWhiteSpace(request.Comment)
            ? null
            : request.Comment.Trim();

        if ((comment?.Length ?? 0) > 600)
            return BadRequest(new { success = false, message = "Bình luận tối đa 600 ký tự." });

        var existingReview = await _context.Set<PoiReview>()
            .FirstOrDefaultAsync(review => review.TouristId == touristId && review.PoiId == request.PoiId, cancellationToken);

        if (existingReview != null)
        {
            existingReview.Rating = request.Rating;
            existingReview.Comment = comment;
            existingReview.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.Set<PoiReview>().Add(new PoiReview
            {
                PoiId = request.PoiId,
                TouristId = touristId,
                Rating = request.Rating,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, message = "Cảm ơn bạn đã gửi đánh giá!" });
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogAudio([FromBody] DuKhachLogAudioRequest request, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var result = await _audioQuota.TryConsumeAsync(
            touristId,
            request.PoiId,
            request.LanguageCode,
            $"web-tourist-{touristId}",
            request.IsTts ? "WebTtsPlay" : "WebAudioPlay",
            cancellationToken: cancellationToken);

        if (!result.Allowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                success = false,
                result.Message,
                result.DailyLimit,
                result.UsedToday,
                result.RemainingToday
            });
        }

        return Ok(new
        {
            success = true,
            result.Message,
            result.IsPremium,
            result.DailyLimit,
            result.UsedToday,
            result.RemainingToday
        });
    }

    private async Task<Dictionary<string, string>> GetActiveLanguagesAsync(CancellationToken cancellationToken)
    {
        var languageRows = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(language => language.IsActive)
            .OrderBy(language => language.LanguageCode == "vi" ? 0 : language.LanguageCode == "en" ? 1 : 2)
            .ThenBy(language => language.LanguageName)
            .ToListAsync(cancellationToken);

        var languages = languageRows
            .Where(language => IsValidLanguageCode(language.LanguageCode))
            .GroupBy(language => NormalizeLanguageCode(language.LanguageCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => GetNativeLanguageDisplayName(group.Key, group.First().LanguageName),
                StringComparer.OrdinalIgnoreCase);

        if (languages.Count == 0)
            EnsureDefaultLanguageOptions(languages);

        return languages;
    }

    private static void EnsureDefaultLanguageOptions(IDictionary<string, string> languages)
    {
        languages.TryAdd("vi", "Tiếng Việt");
        languages.TryAdd("en", "English");
        languages.TryAdd("fr", "Français");
        languages.TryAdd("zh", "中文");
        languages.TryAdd("ja", "日本語");
        languages.TryAdd("ko", "한국어");
    }

    private static List<DuKhachPoiNarrationViewModel> EnsureNarrationsForAllActiveLanguages(
        List<DuKhachPoiNarrationViewModel> narrations,
        IReadOnlyDictionary<string, string> activeLanguages)
    {
        var result = narrations
            .GroupBy(item => NormalizeLanguageCode(item.LanguageCode), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var source = result.FirstOrDefault(item => string.Equals(item.LanguageCode, "en", StringComparison.OrdinalIgnoreCase))
            ?? result.FirstOrDefault(item => string.Equals(item.LanguageCode, "vi", StringComparison.OrdinalIgnoreCase))
            ?? result.FirstOrDefault();

        foreach (var language in activeLanguages)
        {
            var languageCode = NormalizeLanguageCode(language.Key);
            if (result.Any(item => string.Equals(item.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(CreateFallbackNarration(languageCode, language.Value, source));
        }

        return result
            .OrderBy(item => item.LanguageCode == "vi" ? 0 : item.LanguageCode == "en" ? 1 : item.LanguageCode == "zh" ? 2 : item.LanguageCode == "ja" ? 3 : 4)
            .ThenBy(item => item.LanguageName)
            .ToList();
    }

    private static DuKhachPoiNarrationViewModel CreateFallbackNarration(
        string languageCode,
        string languageName,
        DuKhachPoiNarrationViewModel? source)
    {
        var name = string.IsNullOrWhiteSpace(source?.Name) ? "POI" : source!.Name;

        var shortDescription = languageCode switch
        {
            "en" => $"Audio guide for {name} in Ho Chi Minh City.",
            "zh" => $"{name} 的胡志明市语音导览。",
            "ja" => $"{name} のホーチミン市観光音声ガイドです。",
            "ko" => $"{name}의 호치민시 관광 음성 안내입니다.",
            _ => $"Thuyết minh tham quan cho {name}."
        };

        var narrationText = languageCode switch
        {
            "en" => $"This is {name}, a point of interest in Ho Chi Minh City. This audio guide introduces the location, its visitor experience, and useful tips for your trip.",
            "zh" => $"这里是 {name}，胡志明市的一个旅游景点。本语音导览将为您介绍这个地点、参观体验以及旅行时需要注意的提示。",
            "ja" => $"ここは {name}、ホーチミン市の観光スポットです。この音声ガイドでは、場所の特徴、見学のポイント、旅行中に役立つヒントを紹介します。",
            "ko" => $"이곳은 {name}, 호치민시의 관광 명소입니다. 이 음성 안내는 장소의 특징, 관람 포인트, 여행에 도움이 되는 정보를 소개합니다.",
            _ => $"Đây là {name}, một điểm tham quan tại Thành phố Hồ Chí Minh. Phần thuyết minh này giới thiệu khái quát về địa điểm, trải nghiệm tham quan và một số lưu ý hữu ích cho chuyến đi."
        };

        return new DuKhachPoiNarrationViewModel
        {
            LanguageCode = languageCode,
            LanguageName = languageName,
            Name = name,
            ShortDescription = shortDescription,
            FullDescription = narrationText,
            NarrationText = narrationText,
            AudioUrl = null,
            VideoUrl = null
        };
    }

    private static string GetNativeLanguageDisplayName(string? code, string? currentName)
    {
        var normalizedCode = NormalizeLanguageCode(code);
        return normalizedCode switch
        {
            "vi" => "Tiếng Việt",
            "en" => "English",
            "fr" => "Français",
            "zh" or "zh-cn" or "zh-tw" => "中文",
            "ja" => "日本語",
            "ko" => "한국어",
            "th" => "ไทย",
            "de" => "Deutsch",
            "es" => "Español",
            "it" => "Italiano",
            "pt" => "Português",
            "ru" => "Русский",
            _ => string.IsNullOrWhiteSpace(currentName) ? normalizedCode.ToUpperInvariant() : currentName.Trim()
        };
    }

    private string GetSelectedLanguageCode(IEnumerable<string> activeLanguageCodes)
    {
        var activeSet = activeLanguageCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var raw = Request.Query["lang"].FirstOrDefault()
            ?? Request.Cookies["versa.dukhach.lang"]
            ?? "vi";

        var languageCode = NormalizeLanguageCode(raw);
        if (!activeSet.Contains(languageCode))
            languageCode = activeSet.Contains("vi") ? "vi" : activeSet.FirstOrDefault() ?? "vi";

        Response.Cookies.Append("versa.dukhach.lang", languageCode, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });

        return languageCode;
    }

    private static DuKhachPoiNarrationViewModel? SelectNarration(
        IReadOnlyCollection<DuKhachPoiNarrationViewModel> narrations,
        string selectedLanguageCode)
    {
        return narrations.FirstOrDefault(item => string.Equals(item.LanguageCode, selectedLanguageCode, StringComparison.OrdinalIgnoreCase))
            ?? narrations.FirstOrDefault(item => string.Equals(item.LanguageCode, "vi", StringComparison.OrdinalIgnoreCase))
            ?? narrations.FirstOrDefault(item => string.Equals(item.LanguageCode, "en", StringComparison.OrdinalIgnoreCase))
            ?? narrations.FirstOrDefault();
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        var normalized = (languageCode ?? FallbackLanguageCode).Trim().ToLowerInvariant();
        return IsValidLanguageCode(normalized) ? normalized : FallbackLanguageCode;
    }

    private static bool IsValidLanguageCode(string? languageCode)
    {
        var normalized = (languageCode ?? string.Empty).Trim();
        return normalized.Length is >= 2 and <= 10 &&
            normalized.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
    }

    private int? TryGetTouristId()
    {
        if (User.Identity?.IsAuthenticated != true || !User.IsInRole("Tourist"))
            return null;

        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
    }

    private int GetTouristId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<HashSet<int>> GetFeaturedOwnerIdsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var activeSubscriptions = await _context.OwnerSubscriptions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .Where(item =>
                item.Status == "Active" &&
                item.StartsAt <= now &&
                item.ExpiresAt > now &&
                item.PaymentPlan != null &&
                (item.PaymentPlan.Audience == "Owner" || item.PaymentPlan.Audience == "Both"))
            .ToListAsync(cancellationToken);

        var ownerIds = activeSubscriptions
            .Where(item => OwnerFeaturedPlanHelper.IsFeaturedMapPlan(item.PaymentPlan))
            .Select(item => item.OwnerProfileId)
            .ToHashSet();

        // Fallback quan trọng: một số giao dịch đã Paid nhưng subscription chưa được tạo
        // do trước đó code thanh toán/confirm còn lỗi. Vẫn tính nổi bật theo giao dịch đã trả tiền.
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

    private static bool IsFeaturedPoi(Poi poi, HashSet<int> featuredOwnerIds, HashSet<int> featuredPoiIds)
    {
        return (poi.OwnerProfileId.HasValue && featuredOwnerIds.Contains(poi.OwnerProfileId.Value))
            || featuredPoiIds.Contains(poi.Id);
    }

    private async Task<bool> HasActivePremiumAsync(int touristId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.TouristSubscriptions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .AnyAsync(item =>
                item.TouristId == touristId &&
                item.Status == "Active" &&
                item.ExpiresAt > now &&
                item.PaymentPlan != null &&
                (item.PaymentPlan.PlanCode == "USER_PREMIUM" || item.PaymentPlan.Audience == "Tourist" || item.PaymentPlan.Audience == "Both"),
                cancellationToken);
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

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double ToRadians(double value) => value * Math.PI / 180d;
}
