using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class AchievementPage : ContentPage
{
    private AchievementViewModel ViewModel => BindingContext as AchievementViewModel
        ?? throw new InvalidOperationException();

    public AchievementPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<AchievementViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }
}
