using Microsoft.Extensions.DependencyInjection;
using UserMobile.Services;

namespace UserMobile.Views;

public partial class StartupPage : ContentPage
{
    private bool _initialized;

    public StartupPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
            return;

        _initialized = true;
        var localizationService = App.Services.GetRequiredService<ILocalizationService>();
        if (await localizationService.GetSavedLanguageAsync() is null)
        {
            App.ShowLanguageSelectionPage(continueToAuthentication: false);
            return;
        }

        App.ShowMainPage();
    }
}
