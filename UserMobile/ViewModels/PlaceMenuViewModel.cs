using System.Collections.ObjectModel;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class PlaceMenuViewModel : BaseViewModel
{
    private readonly IMenuOrderService _menuOrderService;
    private int _poiId;
    private string _poiTitle = "Gian hàng";
    private string _customerName = string.Empty;
    private string _customerPhone = string.Empty;
    private string _note = string.Empty;
    private string _message = string.Empty;
    private bool _isBusy;
    private MenuOrderDto? _lastOrder;

    public ObservableCollection<MenuCartLineViewModel> Items { get; } = [];
    public ICommand LoadCommand { get; }
    public ICommand SubmitOrderCommand { get; }
    public ICommand OpenMyOrdersCommand { get; }
    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }

    public PlaceMenuViewModel(IMenuOrderService menuOrderService)
    {
        _menuOrderService = menuOrderService;
        LoadCommand = new Command(async () => await LoadAsync());
        SubmitOrderCommand = new Command(async () => await SubmitOrderAsync());
        OpenMyOrdersCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(UserMobile.Views.MenuOrdersPage)));
        IncreaseCommand = new Command<MenuCartLineViewModel>(line => ChangeQuantity(line, 1));
        DecreaseCommand = new Command<MenuCartLineViewModel>(line => ChangeQuantity(line, -1));
    }

    public int PoiId
    {
        get => _poiId;
        set => SetProperty(ref _poiId, value);
    }

    public string PoiTitle
    {
        get => _poiTitle;
        set => SetProperty(ref _poiTitle, value);
    }

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string CustomerPhone
    {
        get => _customerPhone;
        set => SetProperty(ref _customerPhone, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string Message
    {
        get => _message;
        set
        {
            if (SetProperty(ref _message, value))
                OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool HasItems => Items.Count > 0;
    public bool HasNoItems => !HasItems;
    public int SelectedCount => Items.Sum(item => item.Quantity);
    public decimal TotalAmount => Items.Sum(item => item.LineTotal);
    public string TotalText => $"{TotalAmount:N0} VND";
    public bool HasLastOrder => _lastOrder != null;
    public string LastOrderText => _lastOrder == null
        ? string.Empty
        : $"Đã tạo đơn {_lastOrder.OrderCode} • {_lastOrder.TotalAmount:N0} {_lastOrder.Currency}";

    public async Task LoadAsync()
    {
        if (PoiId <= 0 || IsBusy) return;

        try
        {
            IsBusy = true;
            Message = string.Empty;
            Items.Clear();

            var items = await _menuOrderService.GetPoiMenuAsync(PoiId);
            foreach (var item in items)
            {
                var line = new MenuCartLineViewModel(item);
                line.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName is nameof(MenuCartLineViewModel.Quantity) or nameof(MenuCartLineViewModel.LineTotal))
                        RefreshTotals();
                };
                Items.Add(line);
            }

            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasNoItems));
            RefreshTotals();
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SubmitOrderAsync()
    {
        if (IsBusy) return;

        var selected = Items.Where(item => item.Quantity > 0).ToList();
        if (selected.Count == 0)
        {
            Message = "Vui lòng chọn ít nhất 1 món/sản phẩm.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomerPhone))
        {
            Message = "Vui lòng nhập số điện thoại để chủ gian hàng xác nhận đơn.";
            return;
        }

        try
        {
            IsBusy = true;
            Message = string.Empty;

            var request = new CreateMenuOrderRequest
            {
                PoiId = PoiId,
                CustomerName = CustomerName,
                CustomerPhone = CustomerPhone,
                Note = Note,
                Items = selected.Select(item => new CreateMenuOrderItemRequest
                {
                    MenuItemId = item.Id,
                    Quantity = item.Quantity
                }).ToList()
            };

            _lastOrder = await _menuOrderService.CreateOrderAsync(request);
            Message = "Đặt món thành công. Chủ gian hàng sẽ xác nhận đơn.";
            foreach (var item in Items)
                item.Quantity = 0;

            OnPropertyChanged(nameof(HasLastOrder));
            OnPropertyChanged(nameof(LastOrderText));
            RefreshTotals();
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ChangeQuantity(MenuCartLineViewModel? line, int delta)
    {
        if (line == null) return;
        line.Quantity = Math.Clamp(line.Quantity + delta, 0, 20);
        RefreshTotals();
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(TotalText));
    }
}

public sealed class MenuCartLineViewModel : BaseViewModel
{
    private int _quantity;

    public MenuCartLineViewModel(MenuItemDto item)
    {
        Item = item;
        IncreaseSelfCommand = new Command(() => Quantity = Math.Clamp(Quantity + 1, 0, 20));
        DecreaseSelfCommand = new Command(() => Quantity = Math.Clamp(Quantity - 1, 0, 20));
    }

    public ICommand IncreaseSelfCommand { get; }
    public ICommand DecreaseSelfCommand { get; }

    public MenuItemDto Item { get; }
    public int Id => Item.Id;
    public string Name => Item.Name;
    public string Description => Item.Description;
    public string? ImageUrl => Item.ImageUrl;
    public string PriceText => $"{Item.Price:N0} {Item.Currency}";

    public int Quantity
    {
        get => _quantity;
        set
        {
            var normalized = Math.Clamp(value, 0, 20);
            if (SetProperty(ref _quantity, normalized))
            {
                OnPropertyChanged(nameof(LineTotal));
                OnPropertyChanged(nameof(LineTotalText));
            }
        }
    }

    public decimal LineTotal => Item.Price * Quantity;
    public string LineTotalText => $"{LineTotal:N0} {Item.Currency}";
}
