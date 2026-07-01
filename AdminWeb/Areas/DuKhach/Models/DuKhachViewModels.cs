using System.ComponentModel.DataAnnotations;

namespace AdminWeb.Areas.DuKhach.Models;

public sealed class DuKhachRegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(120, ErrorMessage = "Họ tên tối đa 120 ký tự.")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(160, ErrorMessage = "Email tối đa 160 ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class DuKhachLoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}

public sealed class DuKhachProfileViewModel
{
    public int Id { get; init; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(120, ErrorMessage = "Họ tên tối đa 120 ký tự.")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(160, ErrorMessage = "Email tối đa 160 ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    public DateTime CreatedAt { get; init; }
    public int TotalPoints { get; init; }
    public int DiscoveredPoiCount { get; init; }
    public int TotalPoiCount { get; init; }
    public double CompletionPercentage { get; init; }
    public string RankName { get; init; } = "Tân binh";
}

public sealed class DuKhachChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Nhập lại mật khẩu mới")]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class DuKhachForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(160, ErrorMessage = "Email tối đa 160 ký tự.")]
    [Display(Name = "Địa chỉ email")]
    public string Email { get; set; } = "";
}

public sealed class DuKhachResetPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP phải gồm đúng 6 chữ số.")]
    [Display(Name = "Mã OTP")]
    public string Otp { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class DuKhachDashboardViewModel
{
    public int ApprovedPoiCount { get; init; }
    public int TourCount { get; init; }
    public int DiscoveredPoiCount { get; init; }
    public int TotalPoints { get; init; }
    public string RankName { get; init; } = "Tân binh";
    public IReadOnlyList<DuKhachPoiCardViewModel> FeaturedPois { get; init; } = [];
    public IReadOnlyList<DuKhachRecentDiscoveryViewModel> RecentDiscoveries { get; init; } = [];
}

public sealed class DuKhachPoiCardViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string ShortDescription { get; init; } = "";
    public string? CoverImageUrl { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public bool IsFeatured { get; init; }
    public string? OwnerBusinessName { get; init; }
}

public sealed class DuKhachRecentDiscoveryViewModel
{
    public int PoiId { get; init; }
    public string PoiName { get; init; } = "";
    public string Method { get; init; } = "";
    public int Points { get; init; }
    public DateTime DiscoveredAt { get; init; }
}

public sealed class DuKhachMapViewModel
{
    public IReadOnlyList<DuKhachPoiMapItemViewModel> Pois { get; init; } = [];
    public IReadOnlyList<DuKhachLanguageOptionViewModel> Languages { get; init; } = [];
    public string SelectedLanguageCode { get; init; } = "vi";
    public string SelectedLanguageName { get; init; } = "Tiếng Việt";
    public bool IsTouristSignedIn { get; init; }
    public bool HasPremiumActive { get; init; }
    public int AudioDailyLimit { get; init; } = 5;
    public int AudioPlaysUsedToday { get; init; }
    public int? AudioPlaysRemainingToday { get; init; }
    public int DiscoveredCount { get; init; }
    public int TotalPoiCount { get; init; }
    public int FeaturedPoiCount { get; init; }
}

public sealed class DuKhachPoiDetailsPageViewModel
{
    public DuKhachPoiMapItemViewModel Poi { get; init; } = new();
    public IReadOnlyList<DuKhachLanguageOptionViewModel> Languages { get; init; } = [];
    public string SelectedLanguageCode { get; init; } = "vi";
    public string SelectedLanguageName { get; init; } = "Tiếng Việt";
    public bool IsTouristSignedIn { get; init; }
    public bool HasPremiumActive { get; init; }
    public int AudioDailyLimit { get; init; } = 5;
    public int AudioPlaysUsedToday { get; init; }
    public int? AudioPlaysRemainingToday { get; init; }
}

public sealed class DuKhachLanguageOptionViewModel
{
    public string Code { get; init; } = "vi";
    public string Name { get; init; } = "Tiếng Việt";
}

public sealed class DuKhachPoiNarrationViewModel
{
    public string LanguageCode { get; init; } = "vi";
    public string LanguageName { get; init; } = "Tiếng Việt";
    public string Name { get; init; } = "";
    public string ShortDescription { get; init; } = "";
    public string FullDescription { get; init; } = "";
    public string NarrationText { get; init; } = "";
    public string? AudioUrl { get; init; }
    public string? VideoUrl { get; init; }
}

public sealed class DuKhachPoiMapItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string ShortDescription { get; init; } = "";
    public string FullDescription { get; init; } = "";
    public string? CoverImageUrl { get; init; }
    public string? AudioUrl { get; init; }
    public string? VideoUrl { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int Radius { get; init; }
    public bool IsDiscovered { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsShop { get; init; }
    public int MenuItemCount { get; init; }
    public string? OwnerBusinessName { get; init; }
    public bool IsBookmarked { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public string LanguageCode { get; init; } = "vi";
    public string LanguageName { get; init; } = "Tiếng Việt";
    public string NarrationText { get; init; } = "";
    public List<DuKhachPoiNarrationViewModel> Narrations { get; init; } = [];
    public List<DuKhachReviewViewModel> Reviews { get; init; } = [];
}

public sealed class DuKhachCheckInRequest
{
    public int PoiId { get; set; }
    public string? LanguageCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
}

public sealed class DuKhachReviewViewModel
{
    public string TouristName { get; init; } = "";
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class DuKhachChatRequest
{
    public string Message { get; set; } = "";
}

public sealed class DuKhachTourListViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int EstimatedTime { get; init; }
    public int PoiCount { get; init; }
}

public sealed class DuKhachTourDetailViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int EstimatedTime { get; init; }
    public string TravelMode { get; init; } = "";
    public List<DuKhachPoiMapItemViewModel> Pois { get; init; } = [];
}

public sealed class DuKhachLeaderboardItemViewModel
{
    public int Rank { get; init; }
    public string FullName { get; init; } = "";
    public int TotalPoints { get; init; }
    public int DiscoveredCount { get; init; }
    public string RankName { get; init; } = "Tân binh";
}

public sealed class DuKhachSubmitReviewRequest
{
    public int PoiId { get; set; }
    [Range(1, 5)]
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public sealed class DuKhachLogAudioRequest
{
    public int PoiId { get; set; }
    public string? LanguageCode { get; set; }
    public bool IsTts { get; set; }
}
