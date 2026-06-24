using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

[QueryProperty(nameof(PoiId), "poiId")]
[QueryProperty(nameof(PoiTitle), "title")]
public partial class PlaceMenuPage : ContentPage
{
    private PlaceMenuViewModel ViewModel => BindingContext as PlaceMenuViewModel ?? throw new InvalidOperationException();

    public PlaceMenuPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<PlaceMenuViewModel>();
    }

    public string PoiId
    {
        set
        {
            if (int.TryParse(Uri.UnescapeDataString(value ?? string.Empty), out var id))
                ViewModel.PoiId = id;
        }
    }

    public string PoiTitle
    {
        set => ViewModel.PoiTitle = Uri.UnescapeDataString(value ?? "Gian hàng");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }
}
