using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using UserMobile.Models;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class MapPage : ContentPage
{
    private const double DefaultLatitude = 10.7769;
    private const double DefaultLongitude = 106.7009;

    private MapViewModel ViewModel =>
        BindingContext as MapViewModel ?? throw new InvalidOperationException();

    public MapPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<MapViewModel>();
        ViewModel.PlaceSelected += OnPlaceSelected;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
        RenderMap();
    }

    private void RenderMap()
    {
        var places = ViewModel.Places.Select(place => new
        {
            id = place.Id,
            title = place.Title,
            description = place.Description,
            latitude = place.Latitude,
            longitude = place.Longitude
        });
        var placesJson = JsonSerializer.Serialize(places);
        var initialLatitude = ViewModel.Places.FirstOrDefault()?.Latitude ?? DefaultLatitude;
        var initialLongitude = ViewModel.Places.FirstOrDefault()?.Longitude ?? DefaultLongitude;
        var initialZoom = ViewModel.Places.Count > 0 ? 15 : 12;

        MapWebView.Source = new HtmlWebViewSource
        {
            Html = BuildMapHtml(placesJson, initialLatitude, initialLongitude, initialZoom)
        };
    }

    private async void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith("versa://poi/", StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;
        var poiId = Uri.UnescapeDataString(e.Url["versa://poi/".Length..]);
        var place = ViewModel.Places.FirstOrDefault(item =>
            string.Equals(item.Id, poiId, StringComparison.OrdinalIgnoreCase));
        if (place != null)
            await OpenPlaceAsync(place);
    }

    private async void OnPlaceSelected(object? sender, PlaceItem place)
    {
        await OpenPlaceAsync(place);
    }

    private async Task OpenPlaceAsync(PlaceItem place)
    {
        var detailPage = App.Services.GetRequiredService<PlaceDetailPage>();
        await detailPage.LoadPlaceAsync(place);
        await Navigation.PushAsync(detailPage);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PlaceItem place)
            ViewModel.SelectPlace(place);
        PlacesList.SelectedItem = null;
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await ViewModel.LoadAsync(forceRefresh: true);
        RenderMap();
    }

    private async void OnAiClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//AiChatPage");
    }

    private static string BuildMapHtml(
        string placesJson,
        double initialLatitude,
        double initialLongitude,
        int initialZoom)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
                <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"
                      crossorigin="" />
                <style>
                    html, body, #map { width: 100%; height: 100%; margin: 0; background: #eef1f5; }
                    .leaflet-control-attribution { font-size: 10px; }
                    .leaflet-control-zoom { margin-top: 96px !important; }
                    .poi-popup { min-width: 180px; font-family: system-ui, sans-serif; }
                    .poi-popup strong { color: #172033; font-size: 14px; }
                    .poi-popup p { color: #68748a; margin: 6px 0 10px; font-size: 12px; }
                    .poi-popup button {
                        width: 100%; border: 0; border-radius: 10px; padding: 9px 12px;
                        background: #5b3fe4; color: white; font-weight: 600;
                    }
                    .poi-marker {
                        width: 28px; height: 28px; border-radius: 50% 50% 50% 0;
                        background: #5b3fe4; border: 3px solid white;
                        box-shadow: 0 2px 8px rgba(23,32,51,.35);
                        transform: rotate(-45deg);
                    }
                    .poi-marker::after {
                        content: ""; width: 8px; height: 8px; border-radius: 50%;
                        background: white; position: absolute; left: 7px; top: 7px;
                    }
                </style>
            </head>
            <body>
                <div id="map"></div>
                <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"
                        integrity="sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo="
                        crossorigin=""></script>
                <script>
                    const places = {{placesJson}};
                    const map = L.map("map", { zoomControl: true })
                        .setView([{{initialLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, {{initialLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}}], {{initialZoom}});

                    L.tileLayer("https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png", {
                        subdomains: "abcd",
                        maxZoom: 20,
                        attribution: "&copy; OpenStreetMap contributors &copy; CARTO"
                    }).addTo(map);

                    const markerIcon = L.divIcon({
                        className: "",
                        html: '<div class="poi-marker"></div>',
                        iconSize: [34, 34],
                        iconAnchor: [17, 32],
                        popupAnchor: [0, -30]
                    });

                    const bounds = [];
                    for (const place of places) {
                        if (!Number.isFinite(place.latitude) || !Number.isFinite(place.longitude)) continue;
                        const point = [place.latitude, place.longitude];
                        bounds.push(point);

                        const container = document.createElement("div");
                        container.className = "poi-popup";
                        const title = document.createElement("strong");
                        title.textContent = place.title || "Điểm tham quan";
                        const description = document.createElement("p");
                        description.textContent = place.description || "Chạm để xem chi tiết";
                        const button = document.createElement("button");
                        button.textContent = "Mở chi tiết";
                        button.onclick = () => window.location.href = "versa://poi/" + encodeURIComponent(place.id);
                        container.append(title, description, button);

                        L.marker(point, { icon: markerIcon }).addTo(map).bindPopup(container);
                    }

                    if (bounds.length > 1) {
                        map.fitBounds(bounds, { padding: [40, 40], maxZoom: 16 });
                    }
                </script>
            </body>
            </html>
            """;
    }
}
