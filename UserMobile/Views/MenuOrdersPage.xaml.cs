using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;
using UserMobile.Models;

namespace UserMobile.Views;

public partial class MenuOrdersPage : ContentPage
{
    private MenuOrdersViewModel ViewModel => BindingContext as MenuOrdersViewModel ?? throw new InvalidOperationException();

    public MenuOrdersPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<MenuOrdersViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

    private async void OnOrderSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MenuOrderDto order) return;
        OrdersList.SelectedItem = null;
        var page = App.Services.GetRequiredService<MenuOrderDetailPage>();
        page.Load(order);
        await Navigation.PushAsync(page);
    }
}
