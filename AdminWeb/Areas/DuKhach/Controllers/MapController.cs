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
    private readonly AppDbContext _context;

    public MapController(AppDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var touristId = TryGetTouristId();
        var discoveredIds = touristId.HasValue
            ? await _context.TouristPoiDiscoveries
                .AsNoTracking()
                .Where(item => item.TouristId == touristId.Value)
                .Select(item => item.PoiId)
                .ToListAsync(cancellationToken)
            : new List<int>();

        var discoveredSet = discoveredIds.ToHashSet();
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
            Pois = pois.Select(item =>
            {
                var translation = item.Translations.FirstOrDefault(t => t.LanguageCode == "vi")
                    ?? item.Translations.FirstOrDefault();

                return new DuKhachPoiMapItemViewModel
                {
                    Id = item.Id,
                    Name = translation?.Name ?? $"POI #{item.Id}",
                    ShortDescription = translation?.ShortDescription ?? "",
                    FullDescription = translation?.FullDescription ?? "",
                    CoverImageUrl = ToAbsoluteUrl(item.CoverImageUrl),
                    AudioUrl = ToAbsoluteUrl(translation?.AudioUrl),
                    VideoUrl = ToAbsoluteUrl(translation?.VideoUrl),
                    Latitude = (double)item.Latitude,
                    Longitude = (double)item.Longitude,
                    Radius = item.Radius,
                    IsDiscovered = discoveredSet.Contains(item.Id)
                };
            }).ToList()
        };

        return View(model);
    }

    [Authorize(Roles = "Tourist")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn([FromBody] DuKhachCheckInRequest request, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
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

        var poiName = poi.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
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
            LanguageCode = "vi",
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
