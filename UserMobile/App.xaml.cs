using Microsoft.Extensions.DependencyInjection;

namespace UserMobile;

public partial class App : Application
{
    private static IServiceProvider? _services;

    public static IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("Services have not been initialized.");

    public App()
    {
        InitializeComponent();
    }

    public static void InitializeServices(IServiceProvider services)
    {
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new Views.StartupPage());
    }

    public static void ShowMainPage()
    {
        SetRootPage(new AppShell());
    }

    public static void ShowLoginPage()
    {
        var page = Services.GetRequiredService<Views.LoginPage>();
        SetRootPage(new NavigationPage(page)
        {
            BarBackgroundColor = Color.FromArgb("#F7F8FC"),
            BarTextColor = Color.FromArgb("#182033")
        });
    }

    public static void ShowLanguageSelectionPage(bool continueToAuthentication)
    {
        var page = Services.GetRequiredService<Views.LanguageSelectionPage>();
        page.ContinueToAuthentication = continueToAuthentication;
        SetRootPage(new NavigationPage(page)
        {
            BarBackgroundColor = Color.FromArgb("#F7F8FC"),
            BarTextColor = Color.FromArgb("#182033")
        });
    }

    private static void SetRootPage(Page page)
    {
        var window = Current?.Windows.FirstOrDefault();
        if (window is not null)
            window.Page = page;
    }
}
