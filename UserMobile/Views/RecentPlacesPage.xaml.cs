using Microsoft.Extensions.DependencyInjection;
using UserMobile.Models;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class RecentPlacesPage : ContentPage
{
    private RecentPlacesViewModel ViewModel => BindingContext as RecentPlacesViewModel ?? throw new InvalidOperationException();

    public RecentPlacesPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<RecentPlacesViewModel>();
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
            ViewModel.SelectPlaceCommand.Execute(place);
        RecentList.SelectedItem = null;
    }
}
