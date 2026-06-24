using Microsoft.Extensions.DependencyInjection;
using UserMobile.Services;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ILocalizationService _localizationService;
    private ProfileViewModel ViewModel => BindingContext as ProfileViewModel ?? throw new InvalidOperationException();

    public ProfilePage()
    {
        InitializeComponent();
        _localizationService = App.Services.GetRequiredService<ILocalizationService>();
        BindingContext = App.Services.GetRequiredService<ProfileViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

    private async void OnLanguageClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LanguageSelectionPage));
    }

    private async void OnRecentClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecentPlacesPage));
    }

    private async void OnFavoritesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//FavoritesPage");
    }

    private async void OnAchievementsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AchievementPage));
    }

    private async void OnPremiumClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(PremiumPage));

    private async void OnLeaderboardClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(LeaderboardPage));

    private async void OnOrdersClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(MenuOrdersPage));

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void OnEditProfileClicked(object? sender, EventArgs e)
    {
        if (ViewModel.Profile is null)
            return;

        var title = _localizationService.Translate("Profile_Edit");
        var fullName = await DisplayPromptAsync(
            title,
            _localizationService.Translate("Register_FullName"),
            initialValue: ViewModel.Profile.FullName,
            maxLength: 120);

        if (fullName is null)
            return;

        var email = await DisplayPromptAsync(
            title,
            _localizationService.Translate("Register_Email"),
            initialValue: ViewModel.Profile.Email,
            keyboard: Keyboard.Email,
            maxLength: 160);

        if (email is null)
            return;

        var message = await ViewModel.UpdateProfileAsync(fullName, email);
        await DisplayAlertAsync(title, message, _localizationService.Translate("Common_Ok"));
    }

    private async void OnChangePasswordClicked(object? sender, EventArgs e)
    {
        var title = _localizationService.Translate("Profile_ChangePassword");
        var currentPassword = await DisplayPromptAsync(
            title,
            _localizationService.Translate("Login_Password"),
            keyboard: Keyboard.Default);

        if (currentPassword is null)
            return;

        var newPassword = await DisplayPromptAsync(
            title,
            _localizationService.Translate("Profile_ChangePassword"),
            keyboard: Keyboard.Default);

        if (newPassword is null)
            return;

        var confirmPassword = await DisplayPromptAsync(
            title,
            _localizationService.Translate("Register_Password"),
            keyboard: Keyboard.Default);

        if (confirmPassword is null)
            return;

        var message = await ViewModel.ChangePasswordAsync(currentPassword, newPassword, confirmPassword);
        await DisplayAlertAsync(title, message, _localizationService.Translate("Common_Ok"));
    }

    private async void OnForgotPasswordClicked(object? sender, EventArgs e)
    {
        var title = _localizationService.Translate("Profile_ForgotPassword");
        var email = await DisplayPromptAsync(
            title,
            _localizationService.Translate("Login_Email"),
            keyboard: Keyboard.Email,
            maxLength: 160);

        if (email is null)
            return;

        var message = await ViewModel.RequestPasswordResetAsync(email);
        await DisplayAlertAsync(title, message, _localizationService.Translate("Common_Ok"));
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await ViewModel.LogoutAsync();
        await DisplayAlertAsync(
            _localizationService.Translate("Profile_Logout"),
            _localizationService.Translate("Profile_Logout"),
            _localizationService.Translate("Common_Ok"));
    }
}
