using System.ComponentModel.DataAnnotations.Schema;

namespace AdminWeb.Models; // DÀNH CHO WEB

public sealed class Role
{
    public int Id { get; set; }
    public string RoleName { get; set; } = "";
    public string? Description { get; set; }
    public List<User> Users { get; set; } = [];
}

public sealed class User
{
    public int Id { get; set; }
    public int? RoleId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Email { get; set; } = "";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Role? Role { get; set; }
    public OwnerProfile? OwnerProfile { get; set; }
}

public sealed class Tourist
{
    public int Id { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? FullName { get; set; }
    public string AuthProvider { get; set; } = "local";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<Review> Reviews { get; set; } = [];
    public List<TouristFavorite> Favorites { get; set; } = [];
    public List<VisitorPlaybackLog> PlaybackLogs { get; set; } = [];
    public List<TouristPoiDiscovery> PoiDiscoveries { get; set; } = [];
    public List<TouristSubscription> Subscriptions { get; set; } = [];
    public List<MenuOrder> MenuOrders { get; set; } = [];
}

public sealed class Poi
{
    public int Id { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int Radius { get; set; } = 50;
    public string QrCodeToken { get; set; } = "";

    // Workflow duyệt Admin: Pending -> Approved/Rejected
    public string Status { get; set; } = "Pending";

    // Ghi chú/lý do admin khi từ chối (Reject)
    public string? AdminNote { get; set; }

    public int? CreatedBy { get; set; }
    public int? OwnerProfileId { get; set; }
    public string? CoverImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public User? Creator { get; set; }
    public OwnerProfile? OwnerProfile { get; set; }
    public List<PoiTranslation> Translations { get; set; } = [];
    public List<MediaTask> MediaTasks { get; set; } = [];
    public List<MediaAsset> MediaAssets { get; set; } = [];
    public List<Beacon> Beacons { get; set; } = [];
    public List<PoiCategory> PoiCategories { get; set; } = [];
    public List<TourPoi> TourPois { get; set; } = [];
    public List<VisitorPlaybackLog> PlaybackLogs { get; set; } = [];
    public List<TouristPoiDiscovery> Discoveries { get; set; } = [];
    public List<MenuOrder> MenuOrders { get; set; } = [];
}

public sealed class PoiTranslation
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Name { get; set; } = "";
    public string? ShortDescription { get; set; }
    public string? FullDescription { get; set; }

    // Nội dung text dùng để sinh TTS / dubbing
    public string? TtsScript { get; set; }

    // Media output
    public string? AudioUrl { get; set; }
    public string? VideoUrl { get; set; }

    // Audio extra
    public int AudioDuration { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public Poi? Poi { get; set; }
}

public sealed class SupportedLanguage
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(10)]
    public string LanguageCode { get; set; } = ""; // en, ja, ko...

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string LanguageName { get; set; } = "";

    // Edge-TTS voice name, e.g. en-US-AriaNeural
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string EdgeTtsVoice { get; set; } = "";

    public bool IsActive { get; set; } = true;
}

public enum MediaTaskType
{
    TextToAudio = 0,
    VideoDubbing = 1
}

public enum MediaTaskStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    CompletedWithErrors = 3,
    Failed = 4
}

public sealed class MediaTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int PoiId { get; set; }

    public MediaTaskType TaskType { get; set; } = MediaTaskType.TextToAudio;
    public MediaTaskStatus Status { get; set; } = MediaTaskStatus.Pending;

    public int ProgressPercentage { get; set; } = 0;
    public int TotalLanguages { get; set; }
    public int SucceededLanguages { get; set; }
    public int FailedLanguages { get; set; }
    public string? LastError { get; set; }
    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Poi? Poi { get; set; }
}

public sealed class Category
{
    public int Id { get; set; }
    public string? IconUrl { get; set; }
    public string Status { get; set; } = "active";
    public List<CategoryTranslation> Translations { get; set; } = [];
    public List<PoiCategory> PoiCategories { get; set; } = [];
}

public sealed class CategoryTranslation
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Name { get; set; } = "";
    public Category? Category { get; set; }
}

public sealed class PoiCategory
{
    public int PoiId { get; set; }
    public Poi? Poi { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}

public sealed class MediaAsset
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string MediaType { get; set; } = "image";
    public string MediaUrl { get; set; } = "";
    public int SortOrder { get; set; }
    public Poi? Poi { get; set; }
}

public sealed class Beacon
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string? MacAddress { get; set; }
    public string Uuid { get; set; } = "";
    public int Major { get; set; }
    public int Minor { get; set; }
    public string? PlacementNote { get; set; }
    public Poi? Poi { get; set; }
}

public sealed class Tour
{
    public int Id { get; set; }
    public int EstimatedTime { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<TourTranslation> Translations { get; set; } = [];
    public List<TourPoi> TourPois { get; set; } = [];

    [NotMapped]
    public List<int> PoiIds { get; set; } = [];
}

public sealed class TourTranslation
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Tour? Tour { get; set; }
}

public sealed class TourPoi
{
    public int TourId { get; set; }
    public Tour? Tour { get; set; }
    public int PoiId { get; set; }
    public Poi? Poi { get; set; }
    public int SequenceOrder { get; set; }
}

public sealed class Review
{
    public int Id { get; set; }
    public int? TouristId { get; set; }
    public string TargetType { get; set; } = "POI";
    public int TargetId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Tourist? Tourist { get; set; }
}

public sealed class TouristFavorite
{
    public int TouristId { get; set; }
    public Tourist? Tourist { get; set; }
    public string TargetType { get; set; } = "POI";
    public int TargetId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public sealed class VisitorPlaybackLog
{
    public long Id { get; set; }
    public int? TouristId { get; set; }
    public string DeviceId { get; set; } = "";
    public int PoiId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string TriggerType { get; set; } = "GPS";
    public decimal? VisitorLatitude { get; set; }
    public decimal? VisitorLongitude { get; set; }
    public int ListenDuration { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Poi? Poi { get; set; }
    public Tourist? Tourist { get; set; }

    [NotMapped]
    public string? PoiName { get; set; }
}

public sealed class TouristPoiDiscovery
{
    public int TouristId { get; set; }
    public int PoiId { get; set; }
    public string DiscoveryMethod { get; set; } = "GPS";
    public int PointsAwarded { get; set; } = 10;
    public decimal? VisitorLatitude { get; set; }
    public decimal? VisitorLongitude { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    public Tourist? Tourist { get; set; }
    public Poi? Poi { get; set; }
}


public sealed class OwnerProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string BusinessName { get; set; } = "";
    public string? RepresentativeName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public List<Poi> Pois { get; set; } = [];
    public List<OwnerSubscription> Subscriptions { get; set; } = [];
    public List<OwnerMenuItem> MenuItems { get; set; } = [];
    public List<MenuOrder> MenuOrders { get; set; } = [];
}

public sealed class PaymentPlan
{
    public int Id { get; set; }
    public string PlanCode { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string Audience { get; set; } = "Owner"; // Owner / Tourist / Both
    public decimal Price { get; set; }
    public int DurationDays { get; set; } = 30;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public List<PaymentTransaction> Payments { get; set; } = [];
    public List<OwnerSubscription> OwnerSubscriptions { get; set; } = [];
}

public sealed class PaymentTransaction
{
    public int Id { get; set; }
    public string TransactionCode { get; set; } = "";
    public string PayerType { get; set; } = "Owner"; // Owner / Tourist
    public int? OwnerProfileId { get; set; }
    public int? TouristId { get; set; }
    public int? PaymentPlanId { get; set; }
    public string Purpose { get; set; } = "Subscription";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string PaymentMethod { get; set; } = "Manual";
    public string Status { get; set; } = "Pending"; // Pending / Paid / Rejected / Cancelled
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? GatewayOrderCode { get; set; }
    public string? GatewayPaymentLinkId { get; set; }
    public string? GatewayStatus { get; set; }

    public OwnerProfile? OwnerProfile { get; set; }
    public Tourist? Tourist { get; set; }
    public PaymentPlan? PaymentPlan { get; set; }
}

public sealed class OwnerSubscription
{
    public int Id { get; set; }
    public int OwnerProfileId { get; set; }
    public int PaymentPlanId { get; set; }
    public int? PaymentTransactionId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

    public OwnerProfile? OwnerProfile { get; set; }
    public PaymentPlan? PaymentPlan { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }
}

public sealed class TouristSubscription
{
    public int Id { get; set; }
    public int TouristId { get; set; }
    public int PaymentPlanId { get; set; }
    public int? PaymentTransactionId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

    public Tourist? Tourist { get; set; }
    public PaymentPlan? PaymentPlan { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }
}

public sealed class OwnerMenuItem
{
    public int Id { get; set; }
    public int OwnerProfileId { get; set; }
    public int PoiId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = "Active"; // Active / Hidden
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public OwnerProfile? OwnerProfile { get; set; }
    public Poi? Poi { get; set; }
    public List<MenuOrderItem> OrderItems { get; set; } = [];
}

public sealed class MenuOrder
{
    public int Id { get; set; }
    public string OrderCode { get; set; } = "";
    public int TouristId { get; set; }
    public int OwnerProfileId { get; set; }
    public int PoiId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string? Note { get; set; }
    public string Status { get; set; } = "Pending"; // Pending / Confirmed / Preparing / Ready / Completed / Cancelled
    public string PaymentMethod { get; set; } = "PayAtCounter";
    public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid / Paid / Refunded
    public decimal Subtotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public Tourist? Tourist { get; set; }
    public OwnerProfile? OwnerProfile { get; set; }
    public Poi? Poi { get; set; }
    public List<MenuOrderItem> Items { get; set; } = [];
}

public sealed class MenuOrderItem
{
    public int Id { get; set; }
    public int MenuOrderId { get; set; }
    public int OwnerMenuItemId { get; set; }
    public string ItemName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public string Currency { get; set; } = "VND";

    public MenuOrder? MenuOrder { get; set; }
    public OwnerMenuItem? OwnerMenuItem { get; set; }
}

public sealed class PoiOwnerRequest
{
    public int Id { get; set; }
    public int OwnerProfileId { get; set; }
    public int? PoiId { get; set; }
    public string RequestType { get; set; } = "Claim"; // Claim / Create / Update
    public string Status { get; set; } = "Pending";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public OwnerProfile? OwnerProfile { get; set; }
    public Poi? Poi { get; set; }
}

public sealed class SystemSetting
{
    public int Id { get; set; }
    public string SettingKey { get; set; } = "";
    public string? SettingValue { get; set; }
    public string? Description { get; set; }
}

public sealed class AdminActivityLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? TargetTable { get; set; }
    public int? TargetId { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public User? User { get; set; }
}


