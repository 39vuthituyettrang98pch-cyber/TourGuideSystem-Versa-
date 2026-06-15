using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class QrScannerViewModel : BaseViewModel
{
    private readonly IPoiCatalogService _catalogService;
    private string _scanResult = "Chưa có kết quả";
    private bool _isScanned;

    public event EventHandler<PlaceItem>? PlaceScanned;

    public string ScanResult
    {
        get => _scanResult;
        set => SetProperty(ref _scanResult, value);
    }

    public bool IsScanned
    {
        get => _isScanned;
        set => SetProperty(ref _isScanned, value);
    }

    public ICommand ResetScanCommand { get; }

    public QrScannerViewModel(IPoiCatalogService catalogService)
    {
        _catalogService = catalogService;
        ResetScanCommand = new Command(ResetScan);
    }

    public async Task<bool> ProcessScanResultAsync(string qrData)
    {
        IsScanned = true;
        ScanResult = $"Đã quét: {qrData}";

        try
        {
            var place = await _catalogService.FindByQrAsync(qrData);
            if (place != null)
            {
                ScanResult = $"Tìm thấy: {place.Title}";
                PlaceScanned?.Invoke(this, place);
                return true;
            }

            ScanResult = $"Không tìm thấy địa điểm: {qrData}";
        }
        catch (Exception exception)
        {
            ScanResult = $"Không thể tải dữ liệu POI: {exception.Message}";
        }

        await Task.Delay(3000);
        ResetScan();
        return false;
    }

    public void ResetScan()
    {
        ScanResult = "Chưa có kết quả";
        IsScanned = false;
    }
}
