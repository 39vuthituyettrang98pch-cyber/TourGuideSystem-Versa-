using System.Collections.ObjectModel;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class PremiumViewModel : BaseViewModel
{
    private readonly IPremiumService _premiumService;
    private TouristPremiumStatus? _status;
    private bool _isBusy;
    private string _message = string.Empty;

    public ObservableCollection<TouristPaymentPlan> Plans { get; } = [];
    public ObservableCollection<TouristPaymentHistory> Payments { get; } = [];
    public ICommand RefreshCommand { get; }

    public TouristPremiumStatus? Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
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
            SetProperty(ref _message, value);
            OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool HasPayments => Payments.Count > 0;
    public bool HasNoPayments => !HasPayments;

    public PremiumViewModel(IPremiumService premiumService)
    {
        _premiumService = premiumService;
        RefreshCommand = new Command(async () => await LoadAsync());
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            Message = string.Empty;
            Status = await _premiumService.GetStatusAsync();

            Plans.Clear();
            foreach (var plan in await _premiumService.GetPlansAsync())
                Plans.Add(plan);

            Payments.Clear();
            foreach (var payment in await _premiumService.GetPaymentsAsync())
                Payments.Add(payment);
            OnPropertyChanged(nameof(HasPayments));
            OnPropertyChanged(nameof(HasNoPayments));
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

    public async Task<string?> CheckoutAsync(TouristPaymentPlan plan, string method)
    {
        if (IsBusy) return null;
        try
        {
            IsBusy = true;
            Message = string.Empty;
            var response = await _premiumService.CheckoutAsync(plan.Id, method);
            Message = response.Message;
            if (!response.Success || response.Data == null)
                return null;
            return response.Data.CheckoutUrl;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
