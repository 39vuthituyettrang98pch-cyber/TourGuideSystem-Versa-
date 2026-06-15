using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class LanguageSelectionPage : ContentPage
{
    private LanguageSelectionViewModel ViewModel => BindingContext as LanguageSelectionViewModel ?? throw new InvalidOperationException();
    public bool ContinueToAuthentication { get; set; }

    public LanguageSelectionPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<LanguageSelectionViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.InitializeAsync();
    }

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        if (await ViewModel.SaveLanguageAsync())
        {
            if (ContinueToAuthentication)
            {
                App.ShowLoginPage();
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
    }
}
