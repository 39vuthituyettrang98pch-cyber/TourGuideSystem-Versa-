namespace AdminWeb.Contracts.Api;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message) =>
        new() { Success = false, Message = message };
}

public sealed class PoiDto
{
    public int Id { get; init; }
    public string QrCodeToken { get; init; } = "";
    public string Name { get; init; } = "";
    public string ShortDescription { get; init; } = "";
    public string FullDescription { get; init; } = "";
    public string? CoverImageUrl { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Radius { get; init; }
    public IReadOnlyList<int> CategoryIds { get; init; } = [];
    public IReadOnlyList<PoiTranslationDto> Translations { get; init; } = [];
}

public sealed class PoiTranslationDto
{
    public string LanguageCode { get; init; } = "";
    public string LanguageName { get; init; } = "";
    public string Name { get; init; } = "";
    public string ShortDescription { get; init; } = "";
    public string FullDescription { get; init; } = "";
    public string? AudioUrl { get; init; }
    public string? VideoUrl { get; init; }
}

public sealed class CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? IconUrl { get; init; }
    public int PoiCount { get; init; }
}

public sealed class TourDto
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public int DurationMinutes { get; init; }
    public IReadOnlyList<TourPoiDto> Pois { get; init; } = [];
}

public sealed class TourPoiDto
{
    public int Id { get; init; }
    public int SequenceOrder { get; init; }
    public string Name { get; init; } = "";
    public string ShortDescription { get; init; } = "";
    public string FullDescription { get; init; } = "";
    public string? AudioUrl { get; init; }
    public string? VideoUrl { get; init; }
    public string? CoverImageUrl { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Radius { get; init; }
}

public sealed class TouristProfileDto
{
    public int Id { get; init; }
    public string Email { get; init; } = "";
    public string FullName { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}

public sealed class AuthDto
{
    public string AccessToken { get; init; } = "";
    public DateTimeOffset ExpiresAt { get; init; }
    public TouristProfileDto Profile { get; init; } = new();
}

public sealed class FavoriteRequest
{
    public int PoiId { get; init; }
    public bool IsFavorite { get; init; }
}

public sealed class RecentRequest
{
    public int PoiId { get; init; }
}

public sealed class DiscoverPoiRequest
{
    public int PoiId { get; init; }
    public string Method { get; init; } = "GPS";
    public string? QrCodeToken { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? AccuracyMeters { get; init; }
}

public sealed class DiscoveryResultDto
{
    public bool IsNewDiscovery { get; init; }
    public int PointsAwarded { get; init; }
    public string Message { get; init; } = "";
    public IReadOnlyList<AchievementDto> NewlyUnlockedAchievements { get; init; } = [];
    public AchievementSummaryDto Summary { get; init; } = new();
}

public sealed class AchievementSummaryDto
{
    public int TotalPoints { get; init; }
    public int DiscoveredPoiCount { get; init; }
    public int TotalPoiCount { get; init; }
    public double CompletionPercentage { get; init; }
    public string RankName { get; init; } = "";
    public string NextRankName { get; init; } = "";
    public int PointsToNextRank { get; init; }
    public double RankProgress { get; init; }
    public IReadOnlyList<AchievementDto> Achievements { get; init; } = [];
    public IReadOnlyList<RecentDiscoveryDto> RecentDiscoveries { get; init; } = [];
}

public sealed class AchievementDto
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "";
    public int RequiredValue { get; init; }
    public int CurrentValue { get; init; }
    public bool IsUnlocked { get; init; }
    public DateTime? UnlockedAt { get; init; }
}

public sealed class RecentDiscoveryDto
{
    public int PoiId { get; init; }
    public string PoiName { get; init; } = "";
    public string Method { get; init; } = "";
    public int Points { get; init; }
    public DateTime DiscoveredAt { get; init; }
}
