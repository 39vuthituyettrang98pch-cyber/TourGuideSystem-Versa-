using AdminWeb.Areas.DuKhach.Models;
using AdminWeb.Data;
using AdminWeb.Models;
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

    public MapController(AppDbContext context)
    {
        _context = context;
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
            .Where(item =>
                item.Status == "Approved" &&
                item.Latitude >= -90 && item.Latitude <= 90 &&
                item.Longitude >= -180 && item.Longitude <= 180)
            .OrderByDescending(item => item.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var model = new DuKhachMapViewModel
        {
            IsTouristSignedIn = touristId.HasValue,
            DiscoveredCount = discoveredSet.Count,
            TotalPoiCount = pois.Count,
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
            }).ToList()
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
            return BadRequest(new { success = false, message = "Cần bật vị trí để check-in trên bản đồ." });

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
                message = $"Bạn đang cách POI khoảng {Math.Round(distance)}m. Cần vào trong bán kính {Math.Round(allowedDistance)}m để check-in."
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
        var languageCode = NormalizeLanguageCode(request.LanguageCode);

        _context.VisitorPlaybackLogs.Add(new VisitorPlaybackLog
        {
            TouristId = touristId,
            DeviceId = $"web-tourist-{touristId}",
            PoiId = request.PoiId,
            LanguageCode = languageCode,
            TriggerType = "WebAudioPlay",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
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
                group => group.First().LanguageName,
                StringComparer.OrdinalIgnoreCase);

        if (languages.Count == 0)
            EnsureDefaultLanguageOptions(languages);

        return languages;
    }

    private static void EnsureDefaultLanguageOptions(IDictionary<string, string> languages)
    {
        languages.TryAdd("vi", "Tiếng Việt");
        languages.TryAdd("en", "English");
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
