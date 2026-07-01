using Microsoft.Extensions.DependencyInjection;
using UserMobile.Models;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class PremiumPage : ContentPage
{
    private PremiumViewModel ViewModel => (PremiumViewModel)BindingContext;

    public PremiumPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<PremiumViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

    private async void OnBuyClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: TouristPaymentPlan plan }) return;

        var options = new List<string> { "Yêu cầu xác nhận thủ công" };
        if (ViewModel.Status?.VnPayAvailable == true) options.Insert(0, "VNPay");
        if (ViewModel.Status?.MomoAvailable == true) options.Insert(0, ViewModel.Status.MomoDemoMode ? "MoMo demo" : "MoMo");
        var selected = await DisplayActionSheetAsync("Chọn cách thanh toán", "Hủy", null, options.ToArray());
        if (selected is null or "Hủy") return;

        var method = selected.StartsWith("MoMo", StringComparison.Ordinal) ? "MoMo"
            : selected == "VNPay" ? "VNPay" : "Manual";
        var checkoutUrl = await ViewModel.CheckoutAsync(plan, method);
        if (!string.IsNullOrWhiteSpace(checkoutUrl))
            await Browser.Default.OpenAsync(checkoutUrl, BrowserLaunchMode.SystemPreferred);
        else if (ViewModel.HasMessage)
            await DisplayAlertAsync("Gói Premium", ViewModel.Message, "Đã hiểu");
    }
}
