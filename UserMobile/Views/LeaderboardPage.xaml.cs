using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class LeaderboardPage : ContentPage
{
    private LeaderboardViewModel ViewModel => (LeaderboardViewModel)BindingContext;
    public LeaderboardPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<LeaderboardViewModel>();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }
}
