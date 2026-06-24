using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class LoginPage : ContentPage
{
    private LoginViewModel ViewModel => BindingContext as LoginViewModel ?? throw new InvalidOperationException();

    public LoginPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<LoginViewModel>();
        ViewModel.LoginSucceeded += OnLoginSucceeded;
    }

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(App.ShowMainPage);
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(App.Services.GetRequiredService<RegisterPage>());
    }

    private async void OnForgotPasswordClicked(object? sender, EventArgs e)
    {
        var page = App.Services.GetRequiredService<ResetPasswordPage>();
        page.Prepare(ViewModel.Email);
        await Navigation.PushAsync(page);
    }
}
