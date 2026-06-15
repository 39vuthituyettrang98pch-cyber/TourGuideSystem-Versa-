using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class ProfilePage : ContentPage
{
    private ProfileViewModel ViewModel => BindingContext as ProfileViewModel
        ?? throw new InvalidOperationException();

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

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await ViewModel.LogoutAsync();
        App.ShowLoginPage();
    }

    private async void OnLanguageClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LanguageSelectionPage));
    }

    private async void OnRecentClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecentPlacesPage));
    }

    private async void OnFavoritesClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//FavoritesPage");
    }

    private async void OnAchievementsClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AchievementPage));
    }
}
