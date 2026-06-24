using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Services;

public sealed class TouristAudioQuotaService
{
    public const int FreeDailyLimit = 5;

    private static readonly string[] CountedTriggerTypes =
    [
        "WebAudioPlay",
        "WebTtsPlay",
        "MobileAudioPlay",
        "MobileTtsPlay"
    ];

    private readonly AppDbContext _context;

    public TouristAudioQuotaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TouristAudioQuotaStatus> GetStatusAsync(
        int touristId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var isPremium = await HasActivePremiumAsync(touristId, nowUtc, cancellationToken);
        var (startUtc, endUtc) = GetVietnamDayBoundsUtc(nowUtc);
        var usedToday = await CountUsedTodayAsync(touristId, startUtc, endUtc, cancellationToken);

        return BuildStatus(isPremium, usedToday);
    }

    public async Task<TouristAudioQuotaResult> TryConsumeAsync(
        int touristId,
        int poiId,
        string? languageCode,
        string? deviceId,
        string triggerType,
        int listenDuration = 0,
        CancellationToken cancellationToken = default)
    {
        var poiExists = await _context.Pois
            .AsNoTracking()
            .AnyAsync(item => item.Id == poiId && item.Status == "Approved", cancellationToken);
        if (!poiExists)
        {
            return new TouristAudioQuotaResult
            {
                Allowed = false,
                Message = "POI không tồn tại hoặc chưa được duyệt."
            };
        }

        var nowUtc = DateTime.UtcNow;
        var isPremium = await HasActivePremiumAsync(touristId, nowUtc, cancellationToken);
        var (startUtc, endUtc) = GetVietnamDayBoundsUtc(nowUtc);
        var usedToday = await CountUsedTodayAsync(touristId, startUtc, endUtc, cancellationToken);

        if (!isPremium && usedToday >= FreeDailyLimit)
        {
            return new TouristAudioQuotaResult
            {
                Allowed = false,
                IsPremium = false,
                DailyLimit = FreeDailyLimit,
                UsedToday = usedToday,
                RemainingToday = 0,
                Message = $"Bạn đã dùng hết {FreeDailyLimit} lượt nghe miễn phí hôm nay. Nâng cấp Premium để nghe không giới hạn."
            };
        }

        var normalizedTrigger = CountedTriggerTypes.Contains(triggerType, StringComparer.OrdinalIgnoreCase)
            ? triggerType
            : "MobileAudioPlay";

        var log = new VisitorPlaybackLog
        {
            TouristId = touristId,
            DeviceId = NormalizeDeviceId(deviceId, touristId),
            PoiId = poiId,
            LanguageCode = NormalizeLanguageCode(languageCode),
            TriggerType = normalizedTrigger,
            ListenDuration = Math.Clamp(listenDuration, 0, 86_400),
            CreatedAt = nowUtc
        };

        _context.VisitorPlaybackLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        var status = BuildStatus(isPremium, usedToday + 1);
        return new TouristAudioQuotaResult
        {
            Allowed = true,
            IsPremium = status.IsPremium,
            DailyLimit = status.DailyLimit,
            UsedToday = status.UsedToday,
            RemainingToday = status.RemainingToday,
            LogId = log.Id,
            Message = isPremium
                ? "Premium: lượt nghe không giới hạn."
                : $"Đã sử dụng {status.UsedToday}/{FreeDailyLimit} lượt nghe miễn phí hôm nay."
        };
    }

    private async Task<bool> HasActivePremiumAsync(
        int touristId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        return await _context.TouristSubscriptions
            .AsNoTracking()
            .AnyAsync(item =>
                item.TouristId == touristId &&
                item.Status == "Active" &&
                item.StartsAt <= nowUtc &&
                item.ExpiresAt > nowUtc,
                cancellationToken);
    }

    private Task<int> CountUsedTodayAsync(
        int touristId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        return _context.VisitorPlaybackLogs
            .AsNoTracking()
            .CountAsync(item =>
                item.TouristId == touristId &&
                item.CreatedAt >= startUtc &&
                item.CreatedAt < endUtc &&
                CountedTriggerTypes.Contains(item.TriggerType),
                cancellationToken);
    }

    private static TouristAudioQuotaStatus BuildStatus(bool isPremium, int usedToday)
    {
        return new TouristAudioQuotaStatus
        {
            IsPremium = isPremium,
            DailyLimit = FreeDailyLimit,
            UsedToday = usedToday,
            RemainingToday = isPremium ? null : Math.Max(0, FreeDailyLimit - usedToday)
        };
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetVietnamDayBoundsUtc(DateTime nowUtc)
    {
        var timeZone = GetVietnamTimeZone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), timeZone);
        var localStart = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        return (startUtc, startUtc.AddDays(1));
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
    }

    private static string NormalizeDeviceId(string? deviceId, int touristId)
    {
        var value = string.IsNullOrWhiteSpace(deviceId)
            ? $"tourist-{touristId}"
            : deviceId.Trim();
        return value.Length <= 120 ? value : value[..120];
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        var value = string.IsNullOrWhiteSpace(languageCode) ? "vi" : languageCode.Trim().ToLowerInvariant();
        return value.Length <= 10 ? value : value[..10];
    }
}

public class TouristAudioQuotaStatus
{
    public bool IsPremium { get; init; }
    public int DailyLimit { get; init; } = TouristAudioQuotaService.FreeDailyLimit;
    public int UsedToday { get; init; }
    public int? RemainingToday { get; init; }
}

public sealed class TouristAudioQuotaResult : TouristAudioQuotaStatus
{
    public bool Allowed { get; init; }
    public long? LogId { get; init; }
    public string Message { get; init; } = "";
}
