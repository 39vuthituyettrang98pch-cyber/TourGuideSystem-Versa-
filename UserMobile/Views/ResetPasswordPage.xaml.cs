using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class ResetPasswordPage : ContentPage
{
    private ResetPasswordViewModel ViewModel => BindingContext as ResetPasswordViewModel
        ?? throw new InvalidOperationException();

    public ResetPasswordPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<ResetPasswordViewModel>();
        ViewModel.PasswordResetSucceeded += OnPasswordResetSucceeded;
    }

    public void Prepare(string? email)
    {
        ViewModel.Prepare(email);
    }

    private async void OnPasswordResetSucceeded(object? sender, EventArgs e)
    {
        await DisplayAlertAsync(
            "Đổi mật khẩu thành công",
            "Hãy đăng nhập bằng mật khẩu mới trên app hoặc web.",
            "Đăng nhập");

        await Navigation.PopAsync();
    }
}
