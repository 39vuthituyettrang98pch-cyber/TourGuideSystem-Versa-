using System.Windows.Input;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class ResetPasswordViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _email = string.Empty;
    private string _otp = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _message = string.Empty;
    private bool _hasMessage;
    private bool _isOtpSent;
    private bool _isBusy;

    public ResetPasswordViewModel(IAuthService authService)
    {
        _authService = authService;
        SendOtpCommand = new Command(async () => await SendOtpAsync(), CanSendOtp);
        ResetPasswordCommand = new Command(async () => await ResetPasswordAsync(), CanResetPassword);
    }

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
                RefreshCommands();
        }
    }

    public string Otp
    {
        get => _otp;
        set
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).Take(6).ToArray());
            if (SetProperty(ref _otp, digits))
                RefreshCommands();
        }
    }

    public string NewPassword
    {
        get => _newPassword;
        set
        {
            if (SetProperty(ref _newPassword, value))
                RefreshCommands();
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
                RefreshCommands();
        }
    }

    public string Message
    {
        get => _message;
        private set
        {
            SetProperty(ref _message, value);
            HasMessage = !string.IsNullOrWhiteSpace(value);
        }
    }

    public bool HasMessage
    {
        get => _hasMessage;
        private set => SetProperty(ref _hasMessage, value);
    }

    public bool IsOtpSent
    {
        get => _isOtpSent;
        private set
        {
            SetProperty(ref _isOtpSent, value);
            RefreshCommands();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            SetProperty(ref _isBusy, value);
            RefreshCommands();
        }
    }

    public ICommand SendOtpCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public event EventHandler? PasswordResetSucceeded;

    public void Prepare(string? email)
    {
        Email = email?.Trim() ?? string.Empty;
        Otp = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        IsOtpSent = false;
        Message = string.Empty;
    }

    private async Task SendOtpAsync()
    {
        try
        {
            IsBusy = true;
            Message = string.Empty;

            var response = await _authService.RequestPasswordResetAsync(Email);
            Message = response.Message;
            if (!response.Success)
                return;

            IsOtpSent = true;
            if (!string.IsNullOrWhiteSpace(response.Data?.DebugOtp))
                Otp = response.Data.DebugOtp;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResetPasswordAsync()
    {
        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            Message = "Mật khẩu xác nhận không khớp.";
            return;
        }

        try
        {
            IsBusy = true;
            Message = string.Empty;

            var response = await _authService.ResetPasswordAsync(
                Email,
                Otp,
                NewPassword,
                ConfirmPassword);

            Message = response.Message;
            if (response.Success)
                PasswordResetSucceeded?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSendOtp() =>
        !IsBusy && !string.IsNullOrWhiteSpace(Email) && Email.Contains('@');

    private bool CanResetPassword() =>
        !IsBusy &&
        IsOtpSent &&
        Otp.Length == 6 &&
        NewPassword.Length >= 8 &&
        ConfirmPassword.Length >= 8;

    private void RefreshCommands()
    {
        ((Command)SendOtpCommand).ChangeCanExecute();
        ((Command)ResetPasswordCommand).ChangeCanExecute();
    }
}
