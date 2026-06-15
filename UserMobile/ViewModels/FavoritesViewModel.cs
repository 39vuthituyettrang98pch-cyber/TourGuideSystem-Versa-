using System.Collections.ObjectModel;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class FavoritesViewModel : BaseViewModel
{
    private readonly IPoiCatalogService _catalogService;
    private readonly IUserPoiStateService _stateService;
    private readonly IAuthService _authService;
    private bool _isLoggedIn;
    private bool _isEmpty;
    private string _message = string.Empty;

    public event EventHandler<PlaceItem>? PlaceSelected;

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set
        {
            SetProperty(ref _isLoggedIn, value);
            OnPropertyChanged(nameof(IsLoggedOut));
        }
    }
    public bool IsLoggedOut => !IsLoggedIn;

    public ObservableCollection<PlaceItem> Favorites { get; } = [];
    public ICommand SelectFavoriteCommand { get; }
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

    public FavoritesViewModel(
        IPoiCatalogService catalogService,
        IUserPoiStateService stateService,
        IAuthService authService)
    {
        _catalogService = catalogService;
        _stateService = stateService;
        _authService = authService;
        SelectFavoriteCommand = new Command<PlaceItem>(OnFavoriteSelected);
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoggedIn = await _authService.IsLoggedInAsync();
            Favorites.Clear();
            if (!IsLoggedIn)
            {
                IsEmpty = true;
                Message = "Đăng nhập để lưu và xem địa điểm yêu thích.";
                return;
            }

            var places = await _catalogService.GetAllAsync();
            var favoriteIds = await _stateService.GetFavoriteIdsAsync();

            foreach (var place in places.Where(item => favoriteIds.Contains(item.Id)))
                Favorites.Add(place);
            IsEmpty = Favorites.Count == 0;
            Message = IsEmpty ? "Bạn chưa lưu địa điểm yêu thích nào." : string.Empty;
        }
        catch (Exception exception)
        {
            IsEmpty = true;
            Message = "Không thể tải địa điểm yêu thích.";
            System.Diagnostics.Debug.WriteLine($"Could not load favorite POIs: {exception.Message}");
        }
    }

    private void OnFavoriteSelected(PlaceItem place)
    {
        PlaceSelected?.Invoke(this, place);
    }
}
