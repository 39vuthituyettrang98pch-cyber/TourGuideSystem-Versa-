using System.Collections.ObjectModel;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class MapViewModel : BaseViewModel
{
    private readonly IPoiCatalogService _catalogService;
    private readonly IUserPoiStateService _stateService;
    private readonly List<PlaceItem> _allPlaces = [];
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

    public MapViewModel(IPoiCatalogService catalogService, IUserPoiStateService stateService)
    {
        _catalogService = catalogService;
        _stateService = stateService;
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
            var favorites = await _stateService.GetFavoriteIdsAsync();
            _allPlaces.Clear();
            foreach (var place in places)
            {
                place.IsFavorite = favorites.Contains(place.Id);
                _allPlaces.Add(place);
            }
            ApplyFilters();
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

    public void ApplyFilters(
        string? searchText = null,
        bool featuredOnly = false,
        bool highRatedOnly = false,
        bool favoritesOnly = false)
    {
        var query = _allPlaces.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var keyword = searchText.Trim();
            query = query.Where(item =>
                item.Title.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                item.Description.Contains(keyword, StringComparison.CurrentCultureIgnoreCase));
        }
        if (featuredOnly) query = query.Where(item => item.IsFeatured);
        if (highRatedOnly) query = query.Where(item => item.AverageRating >= 4);
        if (favoritesOnly) query = query.Where(item => item.IsFavorite);

        Places.Clear();
        foreach (var place in query
                     .OrderByDescending(item => item.IsFeatured)
                     .ThenByDescending(item => item.AverageRating))
            Places.Add(place);
        IsEmpty = Places.Count == 0;
        Message = IsEmpty ? "Không có địa điểm phù hợp với bộ lọc hiện tại." : string.Empty;
    }
}
