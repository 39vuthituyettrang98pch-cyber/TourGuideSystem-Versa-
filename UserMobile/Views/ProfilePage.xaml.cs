using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class ProfilePage : ContentPage
{
    private ProfileViewModel ViewModel => BindingContext as ProfileViewModel ?? throw new InvalidOperationException();

    public ProfilePage()
    {
        InitializeComponent();
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

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void OnEditProfileClicked(object? sender, EventArgs e)
    {
        if (ViewModel.Profile is null)
            return;

        var fullName = await DisplayPromptAsync(
            "Sửa hồ sơ",
            "Họ tên",
            initialValue: ViewModel.Profile.FullName,
            maxLength: 120);

        if (fullName is null)
            return;

        var email = await DisplayPromptAsync(
            "Sửa hồ sơ",
            "Email",
            initialValue: ViewModel.Profile.Email,
            keyboard: Keyboard.Email,
            maxLength: 160);

        if (email is null)
            return;

        var message = await ViewModel.UpdateProfileAsync(fullName, email);
        await DisplayAlert("Hồ sơ", message, "OK");
    }

    private async void OnChangePasswordClicked(object? sender, EventArgs e)
    {
        var currentPassword = await DisplayPromptAsync(
            "Đổi mật khẩu",
            "Mật khẩu hiện tại",
            placeholder: "Nhập mật khẩu hiện tại",
            keyboard: Keyboard.Default);

        if (currentPassword is null)
            return;

        var newPassword = await DisplayPromptAsync(
            "Đổi mật khẩu",
            "Mật khẩu mới",
            placeholder: "Ít nhất 8 ký tự",
            keyboard: Keyboard.Default);

        if (newPassword is null)
            return;

        var confirmPassword = await DisplayPromptAsync(
            "Đổi mật khẩu",
            "Xác nhận mật khẩu mới",
            keyboard: Keyboard.Default);

        if (confirmPassword is null)
            return;

        var message = await ViewModel.ChangePasswordAsync(currentPassword, newPassword, confirmPassword);
        await DisplayAlert("Đổi mật khẩu", message, "OK");
    }

    private async void OnForgotPasswordClicked(object? sender, EventArgs e)
    {
        var email = await DisplayPromptAsync(
            "Quên mật khẩu",
            "Nhập email tài khoản du khách",
            keyboard: Keyboard.Email,
            maxLength: 160);

        if (email is null)
            return;

        var message = await ViewModel.RequestPasswordResetAsync(email);
        await DisplayAlert("Quên mật khẩu", message, "OK");
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await ViewModel.LogoutAsync();
        await DisplayAlert("Đăng xuất", "Bạn đã đăng xuất khỏi tài khoản.", "OK");
    }
}
