using Microsoft.Extensions.DependencyInjection;
using UserMobile.Models;
using UserMobile.Services;
using UserMobile.ViewModels;
using ZXing.Net.Maui;

namespace UserMobile.Views;

public partial class QrScannerPage : ContentPage
{
    private QrScannerViewModel ViewModel => BindingContext as QrScannerViewModel ?? throw new InvalidOperationException();

    public QrScannerPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<QrScannerViewModel>();
        ViewModel.PlaceScanned += OnPlaceScanned;
        
        BarcodeReader.BarcodesDetected += OnBarcodesDetected;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        BarcodeReader.IsDetecting = status == PermissionStatus.Granted;
        if (status != PermissionStatus.Granted)
            ViewModel.ScanResult = "Cần cấp quyền camera để quét mã QR.";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsDetecting = false;
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
        string? discoveryMessage = null;
        try
        {
            var achievementService = App.Services.GetRequiredService<IAchievementService>();
            var result = await achievementService.DiscoverByQrAsync(place.Id, place.QrCodeToken);
            discoveryMessage = result.BuildDisplayMessage();
        }
        catch (InvalidOperationException exception)
        {
            discoveryMessage = exception.Message;
        }

        var detailPage = App.Services.GetRequiredService<PlaceDetailPage>();
        await detailPage.LoadPlaceAsync(place);
        await Navigation.PushAsync(detailPage);
        if (!string.IsNullOrWhiteSpace(discoveryMessage))
            await DisplayAlertAsync("Khám phá POI", discoveryMessage, "Tuyệt");
    }
}
