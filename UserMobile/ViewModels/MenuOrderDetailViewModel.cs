using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class MenuOrderDetailViewModel : BaseViewModel
{
    private readonly IMenuOrderService _service;
    private MenuOrderDto? _order;
    private bool _isBusy;
    private string _message = string.Empty;

    public event EventHandler<string>? CheckoutRequested;

    public MenuOrderDto? Order
    {
        get => _order;
        set
        {
            SetProperty(ref _order, value);
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanPayOnline));
            OnPropertyChanged(nameof(IsPaid));
        }
    }

    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string Message { get => _message; set { SetProperty(ref _message, value); OnPropertyChanged(nameof(HasMessage)); } }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool CanCancel => Order != null && Order.Status is not ("Completed" or "Cancelled") && Order.PaymentStatus != "Paid";
    public bool CanPayOnline => Order != null && Order.Status != "Cancelled" && Order.PaymentStatus != "Paid";
    public bool IsPaid => Order?.PaymentStatus == "Paid";

    public ICommand CancelCommand { get; }
    public ICommand PayVnPayCommand { get; }
    public ICommand PayMomoCommand { get; }
    public ICommand RefreshCommand { get; }

    public MenuOrderDetailViewModel(IMenuOrderService service)
    {
        _service = service;
        CancelCommand = new Command(async () => await CancelAsync());
        PayVnPayCommand = new Command(async () => await CheckoutAsync("VNPay"));
        PayMomoCommand = new Command(async () => await CheckoutAsync("MoMo"));
        RefreshCommand = new Command(async () => await RefreshAsync());
    }

    public void Load(MenuOrderDto order)
    {
        Order = order;
        Message = string.Empty;
    }

    private async Task CheckoutAsync(string method)
    {
        if (!CanPayOnline || Order == null || IsBusy) return;

        try
        {
            IsBusy = true;
            Message = $"Đang tạo link thanh toán {method}...";
            var result = await _service.CheckoutOrderAsync(Order.Id, method);

            if (!string.IsNullOrWhiteSpace(result.CheckoutUrl))
            {
                Message = "Đã mở cổng thanh toán. Sau khi thanh toán xong, quay lại app và bấm Làm mới.";
                CheckoutRequested?.Invoke(this, result.CheckoutUrl);
            }
            else
            {
                Message = "Đơn sẽ thanh toán tại quầy / khi nhận hàng.";
            }
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAsync()
    {
        if (Order == null || IsBusy) return;

        try
        {
            IsBusy = true;
            Order = await _service.GetOrderAsync(Order.Id);
            Message = "Đã cập nhật trạng thái đơn.";
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CancelAsync()
    {
        if (!CanCancel || Order == null || IsBusy) return;
        try
        {
            IsBusy = true;
            Message = await _service.CancelOrderAsync(Order.Id);
            Order.Status = "Cancelled";
            OnPropertyChanged(nameof(Order));
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanPayOnline));
        }
        catch (InvalidOperationException exception) { Message = exception.Message; }
        finally { IsBusy = false; }
    }
}
