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

    private bool _isPlaceSelectedSubscribed;
    private bool _featuredOnly;
    private bool _ratingOnly;
    private bool _favoritesOnly;
    private bool _isPoiPanelExpanded = true;

    public MapPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<MapViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isPlaceSelectedSubscribed)
        {
            ViewModel.PlaceSelected += OnPlaceSelected;
            _isPlaceSelectedSubscribed = true;
        }

        await ViewModel.LoadAsync();
        RenderMap();
        ApplyPanelLayout();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_isPlaceSelectedSubscribed)
        {
            ViewModel.PlaceSelected -= OnPlaceSelected;
            _isPlaceSelectedSubscribed = false;
        }
    }

    private void RenderMap()
    {
        var places = ViewModel.Places.Select(place => new
        {
            id = place.Id,
            title = place.Title,
            description = place.Description,
            latitude = place.Latitude,
            longitude = place.Longitude,
            imageUrl = place.ImageUrl,
            isFeatured = place.IsFeatured,
            ownerBusinessName = place.OwnerBusinessName,
            rating = place.AverageRating
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
        ApplyPanelLayout();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => ApplyMapFilters();

    private void OnFeaturedFilterClicked(object? sender, EventArgs e)
    {
        _featuredOnly = !_featuredOnly;
        UpdateFilterButton(FeaturedFilterButton, _featuredOnly);
        ApplyMapFilters();
    }

    private void OnRatingFilterClicked(object? sender, EventArgs e)
    {
        _ratingOnly = !_ratingOnly;
        UpdateFilterButton(RatingFilterButton, _ratingOnly);
        ApplyMapFilters();
    }

    private void OnFavoriteFilterClicked(object? sender, EventArgs e)
    {
        _favoritesOnly = !_favoritesOnly;
        UpdateFilterButton(FavoriteFilterButton, _favoritesOnly);
        ApplyMapFilters();
    }

    private void ApplyMapFilters()
    {
        ViewModel.ApplyFilters(SearchEntry.Text, _featuredOnly, _ratingOnly, _favoritesOnly);
        RenderMap();
        ApplyPanelLayout();
    }

    private static void UpdateFilterButton(Button button, bool active)
    {
        button.BackgroundColor = Color.FromArgb(active ? "#5B3FE4" : "#F0EDFF");
        button.TextColor = Color.FromArgb(active ? "#FFFFFF" : "#5B3FE4");
    }

    private async void OnLocateClicked(object? sender, EventArgs e)
    {
        try
        {
            var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                await DisplayAlertAsync("Vị trí", "Hãy cấp quyền vị trí để xem POI gần bạn.", "Đã hiểu");
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(12)));

            if (location == null)
            {
                await DisplayAlertAsync("Vị trí", "Chưa lấy được vị trí hiện tại. Hãy bật GPS và thử lại.", "OK");
                return;
            }

            var lat = location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lng = location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await MapWebView.EvaluateJavaScriptAsync($"window.showUserLocation({lat},{lng})");
        }
        catch (Exception exception) when (exception is FeatureNotSupportedException or PermissionException)
        {
            await DisplayAlertAsync("Vị trí", exception.Message, "Đã hiểu");
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Vị trí", "Không thể lấy vị trí hiện tại. Hãy kiểm tra GPS hoặc quyền vị trí.", "OK");
        }
    }

    private async void OnAiClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//AiChatPage");
    }

    private async void OnTogglePoiPanelClicked(object? sender, EventArgs e)
    {
        _isPoiPanelExpanded = !_isPoiPanelExpanded;
        ApplyPanelLayout();

        await Task.Delay(120);

        try
        {
            var expanded = _isPoiPanelExpanded ? "true" : "false";
            await MapWebView.EvaluateJavaScriptAsync($"window.setPoiPanelExpanded && window.setPoiPanelExpanded({expanded})");
        }
        catch
        {
            // Ignore JavaScript timing issues while the WebView is reloading.
        }
    }

    private void ApplyPanelLayout()
    {
        PoiPanelContent.IsVisible = _isPoiPanelExpanded;
        PoiPanelGrid.RowDefinitions[1].Height = _isPoiPanelExpanded ? new GridLength(190) : new GridLength(0);
        TogglePoiPanelButton.Text = _isPoiPanelExpanded ? "Ẩn" : "Hiện";
        PoiPanelSubtitle.Text = _isPoiPanelExpanded
            ? "Chạm vào thẻ hoặc ghim để xem chi tiết"
            : "Đang ẩn danh sách để xem bản đồ rộng hơn";
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
                    html, body, #map {
                        width: 100%;
                        height: 100%;
                        margin: 0;
                        background: #eef1f5;
                        overflow: hidden;
                    }

                    .leaflet-control-attribution {
                        font-size: 10px;
                    }

                    .leaflet-control-zoom {
                        margin-left: 12px !important;
                        margin-bottom: 305px !important;
                        border: none !important;
                        box-shadow: 0 6px 16px rgba(23,32,51,.20);
                    }

                    .leaflet-control-zoom a {
                        width: 40px !important;
                        height: 40px !important;
                        line-height: 40px !important;
                        font-size: 21px !important;
                        color: #172033 !important;
                    }

                    body.poi-panel-collapsed .leaflet-control-zoom {
                        margin-bottom: 96px !important;
                    }

                    .poi-popup {
                        min-width: 190px;
                        font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                    }

                    .poi-popup strong {
                        color: #172033;
                        font-size: 14px;
                    }

                    .poi-popup p {
                        color: #68748a;
                        margin: 6px 0 10px;
                        font-size: 12px;
                        line-height: 1.35;
                    }

                    .poi-popup button {
                        width: 100%;
                        border: 0;
                        border-radius: 10px;
                        padding: 9px 12px;
                        background: #5b3fe4;
                        color: white;
                        font-weight: 700;
                    }

                    .poi-marker {
                        width: 28px;
                        height: 28px;
                        border-radius: 50% 50% 50% 0;
                        background: #5b3fe4;
                        border: 3px solid white;
                        box-shadow: 0 2px 8px rgba(23,32,51,.35);
                        transform: rotate(-45deg);
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        color: white;
                        font-size: 13px;
                        font-weight: 800;
                    }

                    .poi-marker span {
                        transform: rotate(45deg);
                        margin-top: -2px;
                    }

                    .poi-marker::after {
                        content: "";
                        width: 8px;
                        height: 8px;
                        border-radius: 50%;
                        background: white;
                        position: absolute;
                        left: 7px;
                        top: 7px;
                    }

                    .poi-marker.featured::after {
                        display: none;
                    }

                    .poi-marker.featured {
                        background: #f59e0b;
                        box-shadow: 0 2px 12px rgba(245,158,11,.55);
                    }

                    .poi-marker.nearest {
                        outline: 4px solid rgba(52,211,153,.35);
                        background: #16a36a;
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
                    const map = L.map("map", { zoomControl: false })
                        .setView([{{initialLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, {{initialLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}}], {{initialZoom}});

                    L.control.zoom({ position: "bottomleft" }).addTo(map);

                    L.tileLayer("https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png", {
                        subdomains: "abcd",
                        maxZoom: 20,
                        attribution: "&copy; OpenStreetMap contributors &copy; CARTO"
                    }).addTo(map);

                    const bounds = [];
                    const poiMarkers = [];

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

                        let featuredLine = null;
                        if (place.isFeatured) {
                            featuredLine = document.createElement("p");
                            featuredLine.style.color = "#b45309";
                            featuredLine.style.fontWeight = "800";
                            featuredLine.textContent = place.ownerBusinessName
                                ? "♛ POI nổi bật · " + place.ownerBusinessName
                                : "♛ POI nổi bật";
                        }

                        const button = document.createElement("button");
                        button.textContent = "Mở chi tiết";
                        button.onclick = () => window.location.href = "versa://poi/" + encodeURIComponent(place.id);

                        if (featuredLine) container.append(title, featuredLine, description, button);
                        else container.append(title, description, button);

                        const markerIcon = L.divIcon({
                            className: "",
                            html: `<div class="poi-marker ${place.isFeatured ? "featured" : ""}">${place.isFeatured ? "<span>♛</span>" : ""}</div>`,
                            iconSize: [34, 34],
                            iconAnchor: [17, 32],
                            popupAnchor: [0, -30]
                        });

                        const marker = L.marker(point, { icon: markerIcon })
                            .addTo(map)
                            .bindPopup(container);

                        poiMarkers.push({ place, marker });
                    }

                    if (bounds.length > 1) {
                        map.fitBounds(bounds, { padding: [44, 44], maxZoom: 16 });
                    }

                    let userMarker;

                    window.showUserLocation = (latitude, longitude) => {
                        const userPoint = [latitude, longitude];

                        if (userMarker) map.removeLayer(userMarker);

                        userMarker = L.circleMarker(userPoint, {
                            radius: 9,
                            color: "#fff",
                            weight: 3,
                            fillColor: "#38bdf8",
                            fillOpacity: 1
                        }).addTo(map).bindPopup("Vị trí của bạn");

                        map.setView(userPoint, 16);

                        let nearest = null;
                        let nearestDistance = Number.MAX_VALUE;

                        for (const entry of poiMarkers) {
                            const distance = map.distance(userPoint, [entry.place.latitude, entry.place.longitude]);

                            if (distance < nearestDistance) {
                                nearestDistance = distance;
                                nearest = entry;
                            }

                            const className = entry.place.isFeatured ? "featured" : "";
                            entry.marker.setIcon(L.divIcon({
                                className: "",
                                html: `<div class="poi-marker ${className}">${className.includes("featured") ? "<span>♛</span>" : ""}</div>`,
                                iconSize: [34, 34],
                                iconAnchor: [17, 32],
                                popupAnchor: [0, -30]
                            }));
                        }

                        if (nearest) {
                            const className = nearest.place.isFeatured ? "featured nearest" : "nearest";
                            nearest.marker.setIcon(L.divIcon({
                                className: "",
                                html: `<div class="poi-marker ${className}">${className.includes("featured") ? "<span>♛</span>" : ""}</div>`,
                                iconSize: [34, 34],
                                iconAnchor: [17, 32],
                                popupAnchor: [0, -30]
                            }));

                            nearest.marker.bindTooltip(`Gần bạn nhất · ${Math.round(nearestDistance)} m`, {
                                permanent: false
                            });
                        }
                    };

                    window.setPoiPanelExpanded = (expanded) => {
                        document.body.classList.toggle("poi-panel-collapsed", !expanded);
                        setTimeout(() => map.invalidateSize(), 80);
                    };
                </script>
            </body>
            </html>
            """;
    }
}
