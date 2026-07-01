using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using System.Reflection;
using UserMobile.Services;
using UserMobile.Views;
using ZXing.Net.Maui.Controls;

namespace UserMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
        builder.Services.AddSingleton<ILocalStorageService, LocalStorageService>();
        builder.Services.AddSingleton<IAccessTokenStore, AccessTokenStore>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IUserPoiStateService, UserPoiStateService>();
        builder.Services.AddSingleton<IAchievementService, AchievementService>();
        builder.Services.AddHttpClient<IApiService, ApiService>(client =>
        {
            client.BaseAddress = GetApiBaseAddress();
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<IPoiCatalogService, PoiCatalogService>();
        builder.Services.AddSingleton<IExploreCatalogService, ExploreCatalogService>();
        builder.Services.AddSingleton<IReviewService, ReviewService>();
        builder.Services.AddSingleton<IAiChatService, AiChatService>();
        builder.Services.AddSingleton<IMenuOrderService, MenuOrderService>();
        builder.Services.AddSingleton<IPremiumService, PremiumService>();

        // Audio
        builder.Services.AddSingleton(AudioManager.Current);

        // ViewModels
        builder.Services.AddSingleton<ViewModels.LanguageSelectionViewModel>();
        builder.Services.AddSingleton<ViewModels.MapViewModel>();
        builder.Services.AddSingleton<ViewModels.RecentPlacesViewModel>();
        builder.Services.AddSingleton<ViewModels.QrScannerViewModel>();
        builder.Services.AddSingleton<ViewModels.FavoritesViewModel>();
        builder.Services.AddSingleton<ViewModels.ProfileViewModel>();
        builder.Services.AddSingleton<ViewModels.ToursViewModel>();
        builder.Services.AddSingleton<ViewModels.AchievementViewModel>();
        builder.Services.AddTransient<ViewModels.LoginViewModel>();
        builder.Services.AddTransient<ViewModels.RegisterViewModel>();
        builder.Services.AddTransient<ViewModels.ResetPasswordViewModel>();
        builder.Services.AddTransient<ViewModels.PlaceDetailViewModel>();
        builder.Services.AddSingleton<ViewModels.AiChatViewModel>();
        builder.Services.AddTransient<ViewModels.PlaceMenuViewModel>();
        builder.Services.AddTransient<ViewModels.MenuOrdersViewModel>();
        builder.Services.AddTransient<ViewModels.MenuOrderDetailViewModel>();
        builder.Services.AddSingleton<ViewModels.PremiumViewModel>();
        builder.Services.AddSingleton<ViewModels.LeaderboardViewModel>();

        // Pages
        builder.Services.AddTransient<PlaceDetailPage>();
        builder.Services.AddTransient<LanguageSelectionPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ResetPasswordPage>();
        builder.Services.AddTransient<TourDetailPage>();
        builder.Services.AddTransient<AchievementPage>();
        builder.Services.AddTransient<AiChatPage>();
        builder.Services.AddTransient<PlaceMenuPage>();
        builder.Services.AddTransient<MenuOrdersPage>();
        builder.Services.AddTransient<MenuOrderDetailPage>();
        builder.Services.AddTransient<PremiumPage>();
        builder.Services.AddTransient<LeaderboardPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var mauiApp = builder.Build();
        App.InitializeServices(mauiApp.Services);
        return mauiApp;
    }

    private static Uri GetApiBaseAddress()
    {
        var configuredUrl = typeof(MauiProgram).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(item => item.Key == "ApiBaseUrl")
            ?.Value;

        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredAddress))
        {
#if !DEBUG
            if (configuredAddress.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Production ApiBaseUrl must use HTTPS.");
#endif
            return configuredAddress;
        }

#if DEBUG
#if ANDROID
        return new Uri("http://10.0.2.2:5297/");
#else
        return new Uri("http://localhost:5297/");
#endif
#else
        throw new InvalidOperationException(
            "ApiBaseUrl is required. Build with -p:ApiBaseUrl=https://your-api-host/.");
#endif
    }
}
