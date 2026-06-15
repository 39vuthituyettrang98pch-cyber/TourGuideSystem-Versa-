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
    public string? CoverImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public User? Creator { get; set; }
    public List<PoiTranslation> Translations { get; set; } = [];
    public List<MediaTask> MediaTasks { get; set; } = [];
    public List<MediaAsset> MediaAssets { get; set; } = [];
    public List<Beacon> Beacons { get; set; } = [];
    public List<PoiCategory> PoiCategories { get; set; } = [];
    public List<TourPoi> TourPois { get; set; } = [];
    public List<VisitorPlaybackLog> PlaybackLogs { get; set; } = [];
    public List<TouristPoiDiscovery> Discoveries { get; set; } = [];
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
    [System.ComponentModel.DataAnnotations.Required]
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


