using Microsoft.Extensions.DependencyInjection;
using UserMobile.Models;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class PlaceDetailPage : ContentPage
{
    private PlaceDetailViewModel ViewModel => BindingContext as PlaceDetailViewModel ?? throw new InvalidOperationException();

    public PlaceDetailPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<PlaceDetailViewModel>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    public async Task LoadPlaceAsync(PlaceItem place)
    {
        await ViewModel.LoadPlaceAsync(place);
    }
}
