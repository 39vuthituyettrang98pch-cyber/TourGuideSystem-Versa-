using Microsoft.Extensions.DependencyInjection;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.Views;

public partial class TourDetailPage : ContentPage
{
    private readonly ILocalizationService _localizationService;

    public TourDetailPage()
    {
        InitializeComponent();
        _localizationService = App.Services.GetRequiredService<ILocalizationService>();
    }

    public void LoadTour(TourCatalogDto tour)
    {
        BindingContext = tour;
        Title = tour.Title;
    }

    private async void OnPoiSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TourPoiCatalogDto item)
            return;

        PoiList.SelectedItem = null;
        var language = await _localizationService.GetSavedLanguageAsync();
        var narrationLanguages = new List<NarrationLanguage>();
        if (!string.IsNullOrWhiteSpace(item.AudioUrl) || !string.IsNullOrWhiteSpace(item.VideoUrl))
        {
            narrationLanguages.Add(new NarrationLanguage
            {
                Code = language?.Code ?? "vi",
                Name = language?.Name ?? "Tiếng Việt",
                NativeName = language?.NativeName ?? "Tiếng Việt",
                AudioUrl = item.AudioUrl ?? string.Empty,
                VideoUrl = item.VideoUrl ?? string.Empty
            });
        }

        var place = new PlaceItem
        {
            Id = item.Id.ToString(),
            Title = item.Name,
            Description = item.ShortDescription,
            Introduction = item.FullDescription,
            ImageUrl = item.CoverImageUrl ?? string.Empty,
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            Radius = item.Radius,
            HasNarration = narrationLanguages.Any(entry => !string.IsNullOrWhiteSpace(entry.AudioUrl)),
            NarrationLanguages = narrationLanguages
        };

        var detailPage = App.Services.GetRequiredService<PlaceDetailPage>();
        await detailPage.LoadPlaceAsync(place);
        await Navigation.PushAsync(detailPage);
    }
}
