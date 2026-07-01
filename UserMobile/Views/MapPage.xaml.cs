using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Devices.Sensors;
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
    private PlaceItem? _activeRoutePlace;
    private bool _isListeningLocation;
    private bool _isRoutePlannerMode;
    private readonly List<PlaceItem> _routeStops = [];
    private DateTime _lastRouteRefreshUtc = DateTime.MinValue;

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
        UpdateRoutePlannerPanel();
        await TryStartPendingRouteAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_isPlaceSelectedSubscribed)
        {
            ViewModel.PlaceSelected -= OnPlaceSelected;
            _isPlaceSelectedSubscribed = false;
        }

        StopLocationListening();
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
        if (!e.Url.StartsWith("versa://", StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;

        if (TryGetPlaceFromDeepLink(e.Url, "versa://detail/", out var detailPlace) ||
            TryGetPlaceFromDeepLink(e.Url, "versa://poi/", out detailPlace))
        {
            await OpenPlaceAsync(detailPlace);
            return;
        }

        if (TryGetPlaceFromDeepLink(e.Url, "versa://route/", out var routePlace))
        {
            await StartRouteToPlaceAsync(routePlace);
            return;
        }

        if (TryGetPlaceFromDeepLink(e.Url, "versa://stop/", out var routeStopPlace))
        {
            await ToggleRouteStopAsync(routeStopPlace);
        }
    }

    private bool TryGetPlaceFromDeepLink(string url, string prefix, out PlaceItem place)
    {
        place = null!;
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var poiId = Uri.UnescapeDataString(url[prefix.Length..]);
        var match = ViewModel.Places.FirstOrDefault(item =>
            string.Equals(item.Id, poiId, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            return false;

        place = match;
        return true;
    }

    private async void OnPlaceSelected(object? sender, PlaceItem place)
    {
        await FocusPlaceOnMapAsync(place);
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
        {
            if (_isRoutePlannerMode)
                _ = ToggleRouteStopAsync(place);
            else
                ViewModel.SelectPlace(place);
        }

        PlacesList.SelectedItem = null;
    }

    private async void OnPoiCardTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is PlaceItem place)
        {
            if (_isRoutePlannerMode)
                await ToggleRouteStopAsync(place);
            else
                await FocusPlaceOnMapAsync(place);
        }
    }

    private async void OnPlaceDirectionsClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is PlaceItem place)
            await StartRouteToPlaceAsync(place);
    }

    private async void OnPlaceDetailClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is PlaceItem place)
            await OpenPlaceAsync(place);
    }

    private async void OnAddRouteStopClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is PlaceItem place)
            await ToggleRouteStopAsync(place);
    }

    private async Task FocusPlaceOnMapAsync(PlaceItem place)
    {
        try
        {
            var placeIdJson = JsonSerializer.Serialize(place.Id);
            await MapWebView.EvaluateJavaScriptAsync($"window.focusPoi && window.focusPoi({placeIdJson})");
            if (_isPoiPanelExpanded)
            {
                _isPoiPanelExpanded = false;
                ApplyPanelLayout();
                await MapWebView.EvaluateJavaScriptAsync("window.setPoiPanelExpanded && window.setPoiPanelExpanded(false)");
            }
        }
        catch
        {
            // The WebView can reject JavaScript while it is still reloading. The next tap will work.
        }
    }

    private async Task TryStartPendingRouteAsync()
    {
        var pendingPoiIds = Preferences.Get("pending_route_poi_ids", string.Empty);
        if (!string.IsNullOrWhiteSpace(pendingPoiIds))
        {
            Preferences.Remove("pending_route_poi_ids");

            var ids = pendingPoiIds
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var places = ids
                .Select(id => ViewModel.Places.FirstOrDefault(item =>
                    string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Where(item => item != null)
                .Cast<PlaceItem>()
                .ToList();

            if (places.Count >= 2)
            {
                _routeStops.Clear();
                _routeStops.AddRange(places);
                _isRoutePlannerMode = true;
                _activeRoutePlace = null;
                UpdateRoutePlannerPanel();

                await Task.Delay(450);
                await SyncRouteStopsToMapAsync(drawRoute: true);
                ClearRouteButton.IsVisible = true;
                return;
            }
        }

        var pendingPoiId = Preferences.Get("pending_route_poi_id", string.Empty);
        if (string.IsNullOrWhiteSpace(pendingPoiId))
            return;

        Preferences.Remove("pending_route_poi_id");
        var place = ViewModel.Places.FirstOrDefault(item =>
            string.Equals(item.Id, pendingPoiId, StringComparison.OrdinalIgnoreCase));
        if (place == null)
            return;

        await Task.Delay(450);
        await StartRouteToPlaceAsync(place);
    }

    private async void OnRoutePlannerClicked(object? sender, EventArgs e)
    {
        _isRoutePlannerMode = !_isRoutePlannerMode;
        UpdateRoutePlannerPanel();

        if (_isRoutePlannerMode)
            await DisplayAlertAsync("Lộ trình POI", "Bấm các POI theo thứ tự bạn muốn đi. App sẽ vẽ tuyến trực tiếp trên bản đồ.", "OK");
    }

    private async void OnDrawPoiRouteClicked(object? sender, EventArgs e)
    {
        if (_routeStops.Count < 2)
        {
            await DisplayAlertAsync("Lộ trình POI", "Chọn ít nhất 2 POI để tính đường đi.", "OK");
            return;
        }

        await SyncRouteStopsToMapAsync(drawRoute: true);
        ClearRouteButton.IsVisible = true;
    }

    private async void OnClearPoiRouteStopsClicked(object? sender, EventArgs e)
    {
        await ClearPoiRouteStopsAsync();
    }

    private async Task ToggleRouteStopAsync(PlaceItem place)
    {
        _isRoutePlannerMode = true;
        _activeRoutePlace = null;

        var existingIndex = _routeStops.FindIndex(item =>
            string.Equals(item.Id, place.Id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
            _routeStops.RemoveAt(existingIndex);
        else
            _routeStops.Add(place);

        UpdateRoutePlannerPanel();
        await SyncRouteStopsToMapAsync(drawRoute: _routeStops.Count >= 2);
        ClearRouteButton.IsVisible = _routeStops.Count >= 2;
    }

    private async Task ClearPoiRouteStopsAsync()
    {
        _routeStops.Clear();
        _activeRoutePlace = null;
        UpdateRoutePlannerPanel();
        ClearRouteButton.IsVisible = false;

        try
        {
            await MapWebView.EvaluateJavaScriptAsync("window.setPoiRouteStops && window.setPoiRouteStops([])");
            await MapWebView.EvaluateJavaScriptAsync("window.clearRoute && window.clearRoute()");
        }
        catch
        {
            // Ignore JavaScript timing issues while the WebView is reloading.
        }
    }

    private async Task SyncRouteStopsToMapAsync(bool drawRoute)
    {
        try
        {
            var idsJson = JsonSerializer.Serialize(_routeStops.Select(item => item.Id));
            await MapWebView.EvaluateJavaScriptAsync($"window.setPoiRouteStops && window.setPoiRouteStops({idsJson})");

            if (drawRoute && _routeStops.Count >= 2)
                await MapWebView.EvaluateJavaScriptAsync("window.drawPoiRouteByIds && window.drawPoiRouteByIds()");
        }
        catch
        {
            // The WebView can reject JavaScript while it is still reloading. The next tap will resync.
        }
    }

    private void UpdateRoutePlannerPanel()
    {
        RoutePlannerPanel.IsVisible = _isRoutePlannerMode || _routeStops.Count > 0;
        RoutePlannerButton.BackgroundColor = Color.FromArgb(_isRoutePlannerMode ? "#5B3FE4" : "#F59E0B");
        RouteStopCountLabel.Text = $"{_routeStops.Count} điểm";
        DrawPoiRouteButton.IsEnabled = _routeStops.Count >= 2;
        DrawPoiRouteButton.Opacity = _routeStops.Count >= 2 ? 1 : .55;

        RouteSummaryLabel.Text = _routeStops.Count switch
        {
            0 => "Chọn ít nhất 2 POI",
            1 => $"Điểm đầu: {_routeStops[0].Title}",
            _ => $"Tạm tính đường thẳng {FormatDistance(CalculateRouteDistanceMeters())}"
        };
    }

    private double CalculateRouteDistanceMeters()
    {
        if (_routeStops.Count < 2)
            return 0;

        var totalKm = 0d;
        for (var index = 1; index < _routeStops.Count; index++)
        {
            totalKm += Location.CalculateDistance(
                new Location(_routeStops[index - 1].Latitude, _routeStops[index - 1].Longitude),
                new Location(_routeStops[index].Latitude, _routeStops[index].Longitude),
                DistanceUnits.Kilometers);
        }

        return totalKm * 1000;
    }

    private static string FormatDistance(double meters) =>
        meters >= 1000
            ? $"{meters / 1000:F1} km"
            : $"{Math.Round(meters)} m";

    private async Task StartRouteToPlaceAsync(PlaceItem place)
    {
        try
        {
            _activeRoutePlace = place;
            _routeStops.Clear();
            _isRoutePlannerMode = false;
            UpdateRoutePlannerPanel();
            await SyncRouteStopsToMapAsync(drawRoute: false);

            var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                await DisplayAlertAsync("Dẫn đường", "Hãy cấp quyền vị trí để app vẽ đường đi tới POI.", "Đã hiểu");
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(12)));

            if (location == null)
            {
                await DisplayAlertAsync("Dẫn đường", "Chưa lấy được vị trí hiện tại. Hãy bật GPS và thử lại.", "OK");
                return;
            }

            await DrawRouteAsync(location, place);
            ClearRouteButton.IsVisible = true;
            await StartLocationListeningAsync();
        }
        catch (Exception exception) when (exception is FeatureNotSupportedException or PermissionException)
        {
            await DisplayAlertAsync("Dẫn đường", exception.Message, "Đã hiểu");
        }
        catch
        {
            await DisplayAlertAsync("Dẫn đường", "Không thể lấy GPS để dẫn đường. Hãy kiểm tra quyền vị trí hoặc kết nối mạng.", "OK");
        }
    }

    private async Task DrawRouteAsync(Location location, PlaceItem place)
    {
        var userLat = location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var userLng = location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var placeIdJson = JsonSerializer.Serialize(place.Id);
        _lastRouteRefreshUtc = DateTime.UtcNow;
        await MapWebView.EvaluateJavaScriptAsync($"window.drawRouteToPoi && window.drawRouteToPoi({userLat},{userLng},{placeIdJson})");
    }

    private async Task StartLocationListeningAsync()
    {
        if (_isListeningLocation || Geolocation.Default.IsListeningForeground)
            return;

        Geolocation.Default.LocationChanged += OnLocationChanged;
        var started = await Geolocation.Default.StartListeningForegroundAsync(
            new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(3)));
        _isListeningLocation = started;
    }

    private void StopLocationListening()
    {
        if (!_isListeningLocation && !Geolocation.Default.IsListeningForeground)
            return;

        try
        {
            Geolocation.Default.LocationChanged -= OnLocationChanged;
            Geolocation.Default.StopListeningForeground();
        }
        catch
        {
            // Ignore platform-specific stop errors.
        }
        finally
        {
            _isListeningLocation = false;
        }
    }

    private async void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        try
        {
            var location = e.Location;
            if (_activeRoutePlace != null)
            {
                if (DateTime.UtcNow - _lastRouteRefreshUtc < TimeSpan.FromSeconds(10))
                    return;

                await DrawRouteAsync(location, _activeRoutePlace);
            }
            else
            {
                await ShowUserLocationOnMapAsync(location);
            }
        }
        catch
        {
            // Ignore transient WebView timing errors.
        }
    }

    private async Task ShowUserLocationOnMapAsync(Location location)
    {
        var lat = location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lng = location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await MapWebView.EvaluateJavaScriptAsync($"window.showUserLocation && window.showUserLocation({lat},{lng})");
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await ViewModel.LoadAsync(forceRefresh: true);
        RenderMap();
        ApplyPanelLayout();
        UpdateRoutePlannerPanel();
        await Task.Delay(250);
        await SyncRouteStopsToMapAsync(drawRoute: _routeStops.Count >= 2);
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
        UpdateRoutePlannerPanel();
        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await SyncRouteStopsToMapAsync(drawRoute: _routeStops.Count >= 2));
        });
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

            _activeRoutePlace = null;
            await ShowUserLocationOnMapAsync(location);
            await StartLocationListeningAsync();
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

    private async void OnClearRouteClicked(object? sender, EventArgs e)
    {
        _activeRoutePlace = null;
        ClearRouteButton.IsVisible = false;
        StopLocationListening();

        try
        {
            await MapWebView.EvaluateJavaScriptAsync("window.clearRoute && window.clearRoute()");
        }
        catch
        {
            // Ignore JavaScript timing issues while the WebView is reloading.
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
            ? "Chạm thẻ để đưa map tới POI, hoặc bấm Chi tiết/Chỉ đường"
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

                    .leaflet-control-attribution { font-size: 10px; }

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

                    body.poi-panel-collapsed .leaflet-control-zoom { margin-bottom: 96px !important; }

                    .poi-popup {
                        min-width: 210px;
                        font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                    }

                    .poi-popup strong { color: #172033; font-size: 14px; }

                    .poi-popup p {
                        color: #64748b;
                        margin: 6px 0 10px;
                        font-size: 12px;
                        line-height: 1.35;
                    }

                    .poi-popup .popup-actions { display: grid; gap: 8px; }

                    .poi-popup button {
                        width: 100%;
                        border: 0;
                        border-radius: 10px;
                        padding: 9px 12px;
                        background: #5b3fe4;
                        color: white;
                        font-weight: 800;
                    }

                    .poi-popup button.route { background: linear-gradient(135deg, #34d399, #38bdf8); color: #082f49; }
                    .poi-popup button.google { background: #111827; }

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

                    .poi-marker span { transform: rotate(45deg); margin-top: -2px; }

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

                    .poi-marker.featured::after { display: none; }

                    .poi-marker.featured {
                        width: 34px;
                        height: 34px;
                        background: #f59e0b;
                        box-shadow: 0 2px 14px rgba(245,158,11,.65);
                    }

                    .poi-marker.nearest, .poi-marker.active {
                        outline: 4px solid rgba(52,211,153,.38);
                        background: #16a36a;
                    }

                    .user-dot {
                        width: 18px;
                        height: 18px;
                        border-radius: 50%;
                        background: #38bdf8;
                        border: 4px solid white;
                        box-shadow: 0 0 0 8px rgba(56,189,248,.22), 0 6px 14px rgba(15,23,42,.28);
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
                    let userMarker;
                    let routeLine;
                    let destinationMarker;
                    let activePoiId = null;

                    function markerHtml(place, extraClass = "") {
                        const featured = place.isFeatured ? "featured" : "";
                        const crown = place.isFeatured ? "<span>♛</span>" : "";
                        return `<div class="poi-marker ${featured} ${extraClass}">${crown}</div>`;
                    }

                    function markerIcon(place, extraClass = "") {
                        return L.divIcon({
                            className: "",
                            html: markerHtml(place, extraClass),
                            iconSize: place.isFeatured ? [40, 40] : [34, 34],
                            iconAnchor: place.isFeatured ? [20, 38] : [17, 32],
                            popupAnchor: [0, -30]
                        });
                    }

                    function resetMarkerIcons() {
                        for (const entry of poiMarkers) {
                            const extra = entry.place.id === activePoiId ? "active" : "";
                            entry.marker.setIcon(markerIcon(entry.place, extra));
                        }
                    }

                    function findEntry(id) {
                        return poiMarkers.find(entry => String(entry.place.id) === String(id));
                    }

                    function openGoogleMaps(place, startPoint) {
                        const dest = `${place.latitude},${place.longitude}`;
                        let url = `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(dest)}&travelmode=walking`;
                        if (startPoint) url += `&origin=${encodeURIComponent(startPoint[0] + "," + startPoint[1])}`;
                        window.location.href = url;
                    }

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

                        const actions = document.createElement("div");
                        actions.className = "popup-actions";

                        const routeButton = document.createElement("button");
                        routeButton.className = "route";
                        routeButton.textContent = "🧭 Dẫn đường trong app";
                        routeButton.onclick = () => window.location.href = "versa://route/" + encodeURIComponent(place.id);

                        const detailButton = document.createElement("button");
                        detailButton.textContent = "Mở chi tiết";
                        detailButton.onclick = () => window.location.href = "versa://detail/" + encodeURIComponent(place.id);

                        actions.append(routeButton, detailButton);
                        if (featuredLine) container.append(title, featuredLine, description, actions);
                        else container.append(title, description, actions);

                        const marker = L.marker(point, { icon: markerIcon(place) })
                            .addTo(map)
                            .bindPopup(container);

                        marker.on("click", () => {
                            activePoiId = String(place.id);
                            resetMarkerIcons();
                        });

                        poiMarkers.push({ place, marker });
                    }

                    if (bounds.length > 1) {
                        map.fitBounds(bounds, { padding: [44, 44], maxZoom: 16 });
                    }

                    window.focusPoi = (id) => {
                        const entry = findEntry(id);
                        if (!entry) return;
                        activePoiId = String(id);
                        resetMarkerIcons();
                        map.flyTo([entry.place.latitude, entry.place.longitude], 17, { duration: 0.55 });
                        setTimeout(() => entry.marker.openPopup(), 300);
                    };

                    window.showUserLocation = (latitude, longitude) => {
                        const userPoint = [latitude, longitude];
                        if (userMarker) map.removeLayer(userMarker);

                        userMarker = L.marker(userPoint, {
                            icon: L.divIcon({ className: "", html: '<div class="user-dot"></div>', iconSize: [26, 26], iconAnchor: [13, 13] })
                        }).addTo(map).bindPopup("Vị trí của bạn");

                        if (!routeLine) map.setView(userPoint, 16);

                        let nearest = null;
                        let nearestDistance = Number.MAX_VALUE;
                        for (const entry of poiMarkers) {
                            const distance = map.distance(userPoint, [entry.place.latitude, entry.place.longitude]);
                            if (distance < nearestDistance) {
                                nearestDistance = distance;
                                nearest = entry;
                            }
                        }

                        for (const entry of poiMarkers) {
                            const extra = entry === nearest ? "nearest" : (entry.place.id === activePoiId ? "active" : "");
                            entry.marker.setIcon(markerIcon(entry.place, extra));
                        }

                        if (nearest) {
                            nearest.marker.bindTooltip(`Gần bạn nhất · ${Math.round(nearestDistance)} m`, { permanent: false });
                        }
                    };

                    window.drawRouteToPoi = (latitude, longitude, id) => {
                        const entry = findEntry(id);
                        if (!entry) return;

                        activePoiId = String(id);
                        resetMarkerIcons();

                        const start = [latitude, longitude];
                        const destination = [entry.place.latitude, entry.place.longitude];

                        window.showUserLocation(latitude, longitude);

                        if (routeLine) map.removeLayer(routeLine);
                        if (destinationMarker) map.removeLayer(destinationMarker);

                        routeLine = L.polyline([start, destination], {
                            color: "#0ea5e9",
                            weight: 6,
                            opacity: 0.86,
                            dashArray: "12 10"
                        }).addTo(map);

                        destinationMarker = L.circleMarker(destination, {
                            radius: 11,
                            color: "#ffffff",
                            weight: 3,
                            fillColor: "#22c55e",
                            fillOpacity: 1
                        }).addTo(map).bindPopup("Điểm đến · " + (entry.place.title || "POI"));

                        const distance = Math.round(map.distance(start, destination));
                        entry.marker.bindTooltip(`Đang dẫn đường · ${distance} m`, { permanent: false });
                        entry.marker.openPopup();
                        map.fitBounds([start, destination], { padding: [56, 56], maxZoom: 17 });
                    };

                    window.clearRoute = () => {
                        if (routeLine) map.removeLayer(routeLine);
                        if (destinationMarker) map.removeLayer(destinationMarker);
                        routeLine = null;
                        destinationMarker = null;
                        resetMarkerIcons();
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
