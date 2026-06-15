using Microsoft.Extensions.DependencyInjection;
using UserMobile.Models;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class FavoritesPage : ContentPage
{
    private FavoritesViewModel ViewModel => BindingContext as FavoritesViewModel ?? throw new InvalidOperationException();

    public FavoritesPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<FavoritesViewModel>();
        ViewModel.PlaceSelected += OnPlaceSelected;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

    private async void OnPlaceSelected(object? sender, PlaceItem place)
    {
        var detailPage = App.Services.GetRequiredService<PlaceDetailPage>();
        await detailPage.LoadPlaceAsync(place);
        await Navigation.PushAsync(detailPage);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PlaceItem place)
            ViewModel.SelectFavoriteCommand.Execute(place);
        FavoritesList.SelectedItem = null;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }
}
