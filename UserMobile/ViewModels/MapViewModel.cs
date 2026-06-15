using System.Collections.ObjectModel;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class MapViewModel : BaseViewModel
{
    private readonly IPoiCatalogService _catalogService;
    private bool _isLoading;
    private bool _isEmpty;
    private string _message = string.Empty;

    public event EventHandler<PlaceItem>? PlaceSelected;

    public ObservableCollection<PlaceItem> Places { get; } = [];
    public ICommand SelectPlaceCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        set => SetProperty(ref _isEmpty, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public MapViewModel(IPoiCatalogService catalogService)
    {
        _catalogService = catalogService;
        SelectPlaceCommand = new Command<PlaceItem>(SelectPlace);
    }

    public async Task LoadAsync(bool forceRefresh = false)
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            var places = await _catalogService.GetAllAsync(forceRefresh);
            Places.Clear();
            foreach (var place in places)
                Places.Add(place);
            IsEmpty = Places.Count == 0;
            Message = IsEmpty
                ? "Tạo POI trên Web và duyệt trạng thái Approved để hiển thị tại đây."
                : string.Empty;
        }
        catch (Exception exception)
        {
            Places.Clear();
            IsEmpty = true;
            Message = exception.Message;
            System.Diagnostics.Debug.WriteLine($"Could not load POIs: {exception.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SelectPlace(PlaceItem place)
    {
        PlaceSelected?.Invoke(this, place);
    }
}
