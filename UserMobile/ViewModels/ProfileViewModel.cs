using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public class ProfileViewModel : BaseViewModel
{
    private readonly ILocalizationService _localizationService;
    private readonly IAuthService _authService;
    private readonly IAchievementService _achievementService;
    private string _welcomeText = string.Empty;
    private bool _isLoggedIn;
    private UserProfile? _profile;
    private string _currentLanguage = "Tiếng Việt";
    private AchievementSummary? _achievementSummary;

    public string WelcomeText
    {
        get => _welcomeText;
        set => SetProperty(ref _welcomeText, value);
    }

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set
        {
            SetProperty(ref _isLoggedIn, value);
            OnPropertyChanged(nameof(IsLoggedOut));
        }
    }

    public bool IsLoggedOut => !IsLoggedIn;

    public UserProfile? Profile
    {
        get => _profile;
        set => SetProperty(ref _profile, value);
    }

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set => SetProperty(ref _currentLanguage, value);
    }

    public AchievementSummary? AchievementSummary
    {
        get => _achievementSummary;
        set => SetProperty(ref _achievementSummary, value);
    }

    public ProfileViewModel(
        ILocalizationService localizationService,
        IAuthService authService,
        IAchievementService achievementService)
    {
        _localizationService = localizationService;
        _authService = authService;
        _achievementService = achievementService;
    }

    public async Task LoadAsync()
    {
        Profile = await _authService.GetProfileAsync();
        IsLoggedIn = Profile is not null;
        WelcomeText = IsLoggedIn
            ? $"Xin chào, {Profile!.DisplayName}"
            : "Khám phá địa điểm với tư cách khách";
        CurrentLanguage = (await _localizationService.GetSavedLanguageAsync())?.NativeName ?? "Tiếng Việt";

        AchievementSummary = null;
        if (!IsLoggedIn)
            return;

        try
        {
            AchievementSummary = await _achievementService.GetSummaryAsync();
        }
        catch (InvalidOperationException)
        {
            AchievementSummary = null;
        }
    }

    public async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        await LoadAsync();
    }
}
