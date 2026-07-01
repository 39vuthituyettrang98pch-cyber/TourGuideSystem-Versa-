using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using UserMobile.Models;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class MenuOrderDetailPage : ContentPage
{
    private MenuOrderDetailViewModel ViewModel => (MenuOrderDetailViewModel)BindingContext;

    public MenuOrderDetailPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<MenuOrderDetailViewModel>();
        ViewModel.CheckoutRequested += OnCheckoutRequested;
    }

    public void Load(MenuOrderDto order)
    {
        ViewModel.Load(order);
        Title = order.OrderCode;
    }

    private async void OnCheckoutRequested(object? sender, string checkoutUrl)
    {
        if (string.IsNullOrWhiteSpace(checkoutUrl))
            return;

        await Browser.Default.OpenAsync(checkoutUrl, BrowserLaunchMode.SystemPreferred);
    }
}
