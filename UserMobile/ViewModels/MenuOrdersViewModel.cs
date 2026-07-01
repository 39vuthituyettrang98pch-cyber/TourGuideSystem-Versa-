using System.Collections.ObjectModel;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class MenuOrdersViewModel : BaseViewModel
{
    private readonly IMenuOrderService _menuOrderService;
    private bool _isBusy;
    private string _message = string.Empty;

    public ObservableCollection<MenuOrderDto> Orders { get; } = [];
    public ICommand LoadCommand { get; }

    public MenuOrdersViewModel(IMenuOrderService menuOrderService)
    {
        _menuOrderService = menuOrderService;
        LoadCommand = new Command(async () => await LoadAsync());
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
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
    public bool HasOrders => Orders.Count > 0;
    public bool HasNoOrders => !HasOrders;

    public async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Message = string.Empty;
            Orders.Clear();

            var orders = await _menuOrderService.GetMyOrdersAsync();
            foreach (var order in orders)
                Orders.Add(order);

            OnPropertyChanged(nameof(HasOrders));
            OnPropertyChanged(nameof(HasNoOrders));
        }
        catch (Exception ex)
        {
            Message = ex.Message;
            OnPropertyChanged(nameof(HasOrders));
            OnPropertyChanged(nameof(HasNoOrders));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
