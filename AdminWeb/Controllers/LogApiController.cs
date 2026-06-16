using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Controllers.Api;

[Route("api/log")]
[ApiController]
public sealed class LogApiController : ControllerBase
{
    private static readonly HashSet<string> AllowedTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Open",
        "Manual",
        "Qr",
        "QR",
        "Gps",
        "GPS",
        "Beacon",
        "Audio",
        "Auto"
    };

    private readonly AppDbContext _context;

    public LogApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> PostLog(
        [FromBody] PlaybackLogRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PoiId <= 0 ||
            string.IsNullOrWhiteSpace(request.DeviceId) ||
            request.DeviceId.Length > 120 ||
            !await _context.Pois.AnyAsync(item => item.Id == request.PoiId, cancellationToken))
        {
            return BadRequest(new { success = false, message = "Dữ liệu lịch sử phát không hợp lệ.", data = (object?)null });
        }

        var touristId = GetAuthenticatedTouristId();
        if (touristId.HasValue &&
            !await _context.Tourists.AnyAsync(item => item.Id == touristId.Value, cancellationToken))
        {
            touristId = null;
        }

        var normalizedDeviceId = request.DeviceId.Trim();
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        var recentCount = await _context.VisitorPlaybackLogs
            .CountAsync(item =>
                item.DeviceId == normalizedDeviceId &&
                item.PoiId == request.PoiId &&
                item.CreatedAt >= oneMinuteAgo,
                cancellationToken);

        if (recentCount >= 8)
        {
            return StatusCode(429, new { success = false, message = "Bạn gửi nhật ký quá nhanh. Vui lòng thử lại sau.", data = (object?)null });
        }

        var languageCode = await NormalizeLanguageAsync(request.LanguageCode, cancellationToken);
        var triggerType = NormalizeTriggerType(request.TriggerType);

        var log = new VisitorPlaybackLog
        {
            PoiId = request.PoiId,
            TouristId = touristId,
            DeviceId = normalizedDeviceId,
            LanguageCode = languageCode,
            TriggerType = triggerType,
            ListenDuration = Math.Clamp(request.ListenDuration, 0, 86_400),
            VisitorLatitude = IsValidLatitude(request.VisitorLatitude) ? request.VisitorLatitude : null,
            VisitorLongitude = IsValidLongitude(request.VisitorLongitude) ? request.VisitorLongitude : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.VisitorPlaybackLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã lưu lịch sử phát audio.", data = new { log.Id } });
    }

    private int? GetAuthenticatedTouristId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var touristId) ? touristId : null;
    }

    private async Task<string> NormalizeLanguageAsync(string? languageCode, CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(languageCode)
            ? "vi"
            : languageCode.Trim().ToLowerInvariant();

        var isActive = await _context.SupportedLanguages
            .AsNoTracking()
            .AnyAsync(language => language.IsActive && language.LanguageCode == code, cancellationToken);

        return isActive ? code : "vi";
    }

    private static string NormalizeTriggerType(string? triggerType)
    {
        var value = string.IsNullOrWhiteSpace(triggerType)
            ? "Audio"
            : triggerType.Trim();

        return AllowedTriggerTypes.Contains(value) ? value : "Audio";
    }

    private static bool IsValidLatitude(decimal? value) =>
        value is >= -90 and <= 90;

    private static bool IsValidLongitude(decimal? value) =>
        value is >= -180 and <= 180;
}

public sealed class PlaybackLogRequest
{
    public int PoiId { get; set; }

    // Ignored on purpose. The API gets TouristId from the JWT to prevent spoofing.
    public int? TouristId { get; set; }

    public string DeviceId { get; set; } = "";
    public string? LanguageCode { get; set; }
    public string? TriggerType { get; set; }
    public int ListenDuration { get; set; }
    public decimal? VisitorLatitude { get; set; }
    public decimal? VisitorLongitude { get; set; }
    public DateTime? CreatedAt { get; set; }
}
