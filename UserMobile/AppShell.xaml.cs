using Microsoft.Extensions.DependencyInjection;
using UserMobile.Services;
using UserMobile.Views;

namespace UserMobile;

public partial class AppShell : Shell
{
    private readonly ILocalizationService _localizationService;
    private readonly IAuthService _authService;

    public AppShell()
    {
        InitializeComponent();
        _localizationService = App.Services?.GetRequiredService<ILocalizationService>() ?? throw new InvalidOperationException("Services not initialized.");
        _authService = App.Services.GetRequiredService<IAuthService>();
        Routing.RegisterRoute(nameof(LanguageSelectionPage), typeof(LanguageSelectionPage));
        Routing.RegisterRoute(nameof(PlaceDetailPage), typeof(PlaceDetailPage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(RecentPlacesPage), typeof(RecentPlacesPage));
        Routing.RegisterRoute(nameof(AchievementPage), typeof(AchievementPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var savedLanguage = await _localizationService.GetSavedLanguageAsync();
        if (savedLanguage is null)
        {
            App.ShowLanguageSelectionPage(continueToAuthentication: true);
            return;
        }

        if (!await _authService.IsLoggedInAsync())
            App.ShowLoginPage();
    }
}
