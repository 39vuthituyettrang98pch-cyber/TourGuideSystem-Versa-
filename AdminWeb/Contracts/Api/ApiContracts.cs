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


public sealed class LanguageDto
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string NativeName { get; init; } = "";
    public string EdgeTtsVoice { get; init; } = "";
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
    public double AverageRating { get; init; }
    public int RatingCount { get; init; }
    public bool IsFeatured { get; init; }
    public string? OwnerBusinessName { get; init; }
    public IReadOnlyList<ReviewDto> RecentReviews { get; init; } = [];
    public IReadOnlyList<int> CategoryIds { get; init; } = [];
    public IReadOnlyList<PoiTranslationDto> Translations { get; init; } = [];
}

public sealed class TouristPremiumDto
{
    public bool IsPremium { get; init; }
    public string PlanName { get; init; } = "";
    public DateTime? ExpiresAt { get; init; }
    public bool VnPayAvailable { get; init; }
    public bool MomoAvailable { get; init; }
    public bool MomoDemoMode { get; init; }
}

public sealed class TouristPaymentPlanDto
{
    public int Id { get; init; }
    public string PlanCode { get; init; } = "";
    public string PlanName { get; init; } = "";
    public decimal Price { get; init; }
    public int DurationDays { get; init; }
    public string Description { get; init; } = "";
}

public sealed class TouristPaymentDto
{
    public int Id { get; init; }
    public string TransactionCode { get; init; } = "";
    public string PlanName { get; init; } = "";
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public string PaymentMethod { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
}

public sealed class TouristCheckoutRequest
{
    public int PlanId { get; init; }
    public string PaymentMethod { get; init; } = "Manual";
}

public sealed class TouristCheckoutDto
{
    public int PaymentId { get; init; }
    public string Status { get; init; } = "";
    public string? CheckoutUrl { get; init; }
}

public sealed class LeaderboardItemDto
{
    public int Rank { get; init; }
    public string DisplayName { get; init; } = "";
    public int TotalPoints { get; init; }
    public int DiscoveredPoiCount { get; init; }
    public string RankName { get; init; } = "";
    public bool IsCurrentUser { get; init; }
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

public sealed class ReviewDto
{
    public int Id { get; init; }
    public int PoiId { get; init; }
    public int TouristId { get; init; }
    public string TouristName { get; init; } = "";
    public int Rating { get; init; }
    public string Comment { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}

public sealed class ReviewSummaryDto
{
    public int PoiId { get; init; }
    public double AverageRating { get; init; }
    public int RatingCount { get; init; }
    public ReviewDto? MyReview { get; init; }
    public IReadOnlyList<ReviewDto> Reviews { get; init; } = [];
}

public sealed class SubmitReviewRequest
{
    public int Rating { get; init; }
    public string? Comment { get; init; }
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

public sealed class AudioPlayAccessRequestDto
{
    public int PoiId { get; init; }
    public string LanguageCode { get; init; } = "vi";
    public string DeviceId { get; init; } = "";
    public bool IsTts { get; init; }
}

public sealed class AudioPlayAccessDto
{
    public bool IsPremium { get; init; }
    public int DailyLimit { get; init; }
    public int UsedToday { get; init; }
    public int? RemainingToday { get; init; }
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

public sealed class GpsCheckInRequest
{
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

public sealed class AiChatRequestDto
{
    public string? Message { get; init; }
    public string? LanguageCode { get; init; }
    public string? CurrentScreen { get; init; }
}

public sealed class AiChatReplyDto
{
    public string Reply { get; init; } = "";
    public string LanguageCode { get; init; } = "vi";
    public string Source { get; init; } = "fallback";
}


public sealed class MenuItemDto
{
    public int Id { get; init; }
    public int PoiId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal Price { get; init; }
    public string Currency { get; init; } = "VND";
    public string? ImageUrl { get; init; }
}

public sealed class CreateMenuOrderRequest
{
    public int PoiId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<CreateMenuOrderItemRequest> Items { get; init; } = [];
}

public sealed class CreateMenuOrderItemRequest
{
    public int MenuItemId { get; init; }
    public int Quantity { get; init; }
}

public sealed class MenuOrderDto
{
    public int Id { get; init; }
    public string OrderCode { get; init; } = "";
    public int PoiId { get; init; }
    public string PoiName { get; init; } = "";
    public string OwnerName { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public string Status { get; init; } = "";
    public string PaymentMethod { get; init; } = "";
    public string PaymentStatus { get; init; } = "";
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "VND";
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<MenuOrderItemDto> Items { get; init; } = [];
}


public sealed class MenuOrderCheckoutRequest
{
    public string PaymentMethod { get; init; } = "PayAtCounter";
}

public sealed class MenuOrderCheckoutDto
{
    public int PaymentId { get; init; }
    public int OrderId { get; init; }
    public string PaymentMethod { get; init; } = "";
    public string Status { get; init; } = "";
    public string? CheckoutUrl { get; init; }
}

public sealed class MenuOrderItemDto
{
    public int Id { get; init; }
    public int MenuItemId { get; init; }
    public string ItemName { get; init; } = "";
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal LineTotal { get; init; }
    public string Currency { get; init; } = "VND";
}
