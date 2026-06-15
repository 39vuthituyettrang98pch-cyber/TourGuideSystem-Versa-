using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class RegisterPage : ContentPage
{
    private RegisterViewModel ViewModel => BindingContext as RegisterViewModel
        ?? throw new InvalidOperationException();

    public RegisterPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<RegisterViewModel>();
        ViewModel.RegistrationSucceeded += OnRegistrationSucceeded;
    }

    private async void OnRegistrationSucceeded(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(App.ShowMainPage);
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
