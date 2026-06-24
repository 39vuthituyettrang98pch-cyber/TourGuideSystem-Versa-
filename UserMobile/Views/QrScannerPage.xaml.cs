using Microsoft.Extensions.DependencyInjection;
using UserMobile.Models;
using UserMobile.Services;
using UserMobile.ViewModels;
using ZXing.Net.Maui;

namespace UserMobile.Views;

public partial class QrScannerPage : ContentPage
{
    private QrScannerViewModel ViewModel => BindingContext as QrScannerViewModel ?? throw new InvalidOperationException();

    private bool _eventsSubscribed;

    public QrScannerPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<QrScannerViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SubscribeEvents();
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        BarcodeReader.IsDetecting = status == PermissionStatus.Granted;
        if (status != PermissionStatus.Granted)
            ViewModel.ScanResult = "Cần cấp quyền camera để quét mã QR.";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsDetecting = false;
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (_eventsSubscribed)
            return;

        ViewModel.PlaceScanned += OnPlaceScanned;
        BarcodeReader.BarcodesDetected += OnBarcodesDetected;
        _eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_eventsSubscribed)
            return;

        ViewModel.PlaceScanned -= OnPlaceScanned;
        BarcodeReader.BarcodesDetected -= OnBarcodesDetected;
        _eventsSubscribed = false;
    }

    private async void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (e.Results?.Length > 0)
        {
            BarcodeReader.IsDetecting = false;
            var result = e.Results[0].Value;

            // Process the scanned code in the main thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (!string.IsNullOrEmpty(result))
                {
                    var found = await ViewModel.ProcessScanResultAsync(result);
                    if (!found)
                        BarcodeReader.IsDetecting = true;
                }
                else
                {
                    BarcodeReader.IsDetecting = true;
                }
            });
        }
    }

    private async void OnPlaceScanned(object? sender, PlaceItem place)
    {
        var detailPage = App.Services.GetRequiredService<PlaceDetailPage>();
        await detailPage.LoadPlaceAsync(place);
        await Navigation.PushAsync(detailPage);
        await DisplayAlertAsync(
            "QR POI",
            "QR chỉ dùng để mở chi tiết POI. Muốn nhận điểm, hãy bấm Check-in bằng GPS khi bạn đang ở gần địa điểm.",
            "Đã hiểu");
    }
}
