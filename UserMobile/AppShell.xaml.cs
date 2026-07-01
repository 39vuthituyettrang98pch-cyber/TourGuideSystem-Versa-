using Microsoft.Extensions.DependencyInjection;
using UserMobile.Services;
using UserMobile.Views;

namespace UserMobile;

public partial class AppShell : Shell
{
    private readonly ILocalizationService _localizationService;

    public AppShell()
    {
        InitializeComponent();
        _localizationService = App.Services.GetRequiredService<ILocalizationService>();
        _localizationService.LanguageChanged += OnLanguageChanged;

        RegisterRoutes();

        ApplyShellTranslations();
    }


    private static void RegisterRoutes()
    {
        Routing.RegisterRoute(nameof(LanguageSelectionPage), typeof(LanguageSelectionPage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(PlaceDetailPage), typeof(PlaceDetailPage));
        Routing.RegisterRoute(nameof(RecentPlacesPage), typeof(RecentPlacesPage));
        Routing.RegisterRoute(nameof(AchievementPage), typeof(AchievementPage));
        Routing.RegisterRoute(nameof(PlaceMenuPage), typeof(PlaceMenuPage));
        Routing.RegisterRoute(nameof(MenuOrdersPage), typeof(MenuOrdersPage));
        Routing.RegisterRoute(nameof(TourDetailPage), typeof(TourDetailPage));
        Routing.RegisterRoute(nameof(PremiumPage), typeof(PremiumPage));
        Routing.RegisterRoute(nameof(LeaderboardPage), typeof(LeaderboardPage));
        Routing.RegisterRoute(nameof(MenuOrderDetailPage), typeof(MenuOrderDetailPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var savedLanguage = await _localizationService.GetSavedLanguageAsync();
        if (savedLanguage is null)
        {
            App.ShowLanguageSelectionPage(continueToAuthentication: false);
            return;
        }

        ApplyShellTranslations();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(ApplyShellTranslations);
    }

    private void ApplyShellTranslations()
    {
        Title = _localizationService.Translate("App_Title");
        ExploreTab.Title = _localizationService.Translate("Tab_Explore");
        ToursTab.Title = _localizationService.Translate("Tab_Tour");
        QrTab.Title = _localizationService.Translate("Tab_Qr");
        FavoritesTab.Title = _localizationService.Translate("Tab_Favorites");
        AiTab.Title = _localizationService.Translate("Tab_Ai");
        ProfileTab.Title = _localizationService.Translate("Tab_Profile");
    }
}
