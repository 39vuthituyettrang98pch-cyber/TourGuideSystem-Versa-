using System.Collections.ObjectModel;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class RecentPlacesViewModel : BaseViewModel
{
    private readonly IPoiCatalogService _catalogService;
    private readonly IUserPoiStateService _stateService;
    private bool _isEmpty;
    private string _message = string.Empty;

    public event EventHandler<PlaceItem>? PlaceSelected;
    public ObservableCollection<PlaceItem> RecentPlaces { get; } = [];
    public ICommand SelectPlaceCommand { get; }
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

    public RecentPlacesViewModel(IPoiCatalogService catalogService, IUserPoiStateService stateService)
    {
        _catalogService = catalogService;
        _stateService = stateService;
        SelectPlaceCommand = new Command<PlaceItem>(OnPlaceSelected);
    }

    public async Task LoadAsync()
    {
        try
        {
            var places = await _catalogService.GetAllAsync();
            var placesById = places.ToDictionary(place => place.Id, StringComparer.OrdinalIgnoreCase);
            var recentIds = await _stateService.GetRecentIdsAsync();
            RecentPlaces.Clear();
            foreach (var id in recentIds)
            {
                if (placesById.TryGetValue(id, out var place))
                    RecentPlaces.Add(place);
            }
            IsEmpty = RecentPlaces.Count == 0;
            Message = IsEmpty ? "Bạn chưa mở địa điểm nào gần đây." : string.Empty;
        }
        catch (Exception exception)
        {
            IsEmpty = true;
            Message = "Không thể tải lịch sử gần đây.";
            System.Diagnostics.Debug.WriteLine($"Could not load recent POIs: {exception.Message}");
        }
    }

    private void OnPlaceSelected(PlaceItem place)
    {
        PlaceSelected?.Invoke(this, place);
    }
}
