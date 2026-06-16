using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;
using UserMobile.Services;

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
        var email = await DisplayPromptAsync(
            "Quên mật khẩu",
            "Nhập email tài khoản du khách",
            initialValue: ViewModel.Email,
            keyboard: Keyboard.Email,
            maxLength: 160);

        if (email is null)
            return;

        var result = await App.Services.GetRequiredService<IAuthService>()
            .RequestPasswordResetAsync(email);

        await DisplayAlert("Quên mật khẩu", result.Message, "OK");
    }
}
