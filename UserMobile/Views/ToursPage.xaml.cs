using Microsoft.Extensions.DependencyInjection;
using UserMobile.Models;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class ToursPage : ContentPage
{
    private ToursViewModel ViewModel =>
        BindingContext as ToursViewModel ?? throw new InvalidOperationException();

    public ToursPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<ToursViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

    private async void OnCategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CategoryCatalogDto category)
            await ViewModel.SelectCategoryAsync(category);
    }

    private async void OnClearFilterClicked(object? sender, EventArgs e)
    {
        CategoryList.SelectedItem = null;
        await ViewModel.SelectCategoryAsync(null);
    }

    private async void OnTourSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TourCatalogDto tour)
            return;

        TourList.SelectedItem = null;
        var detailPage = App.Services.GetRequiredService<TourDetailPage>();
        detailPage.LoadTour(tour);
        await Navigation.PushAsync(detailPage);
    }

    private async void OnPremiumClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(PremiumPage));
}
