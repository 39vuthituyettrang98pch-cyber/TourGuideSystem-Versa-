using System.Net.Mail;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class RegisterViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _message = string.Empty;
    private bool _isBusy;

    public event EventHandler? RegistrationSucceeded;

    public string FullName
    {
        get => _fullName;
        set
        {
            if (SetProperty(ref _fullName, value))
                ClearMessage();
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
                ClearMessage();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
                ClearMessage();
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
                ClearMessage();
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            SetProperty(ref _message, value);
            OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool IsBusy
    {
        get => _isBusy;
        set { SetProperty(ref _isBusy, value); RefreshCommand(); }
    }

    public ICommand RegisterCommand { get; }

    public RegisterViewModel(IAuthService authService)
    {
        _authService = authService;
        RegisterCommand = new Command(async () => await RegisterAsync(), () => !IsBusy);
    }

    public async Task<AuthResult> RegisterAsync()
    {
        var validationMessage = ValidateInput();
        if (validationMessage is not null)
            return ValidationFailure(validationMessage);

        try
        {
            IsBusy = true;
            Message = string.Empty;
            var result = await _authService.RegisterAsync(FullName, Email, Password);
            Message = result.Message;
            if (result.IsSuccess)
                RegistrationSucceeded?.Invoke(this, EventArgs.Empty);
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string? ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(FullName))
            return "Vui lòng nhập họ và tên.";

        if (string.IsNullOrWhiteSpace(Email))
            return "Vui lòng nhập email.";

        var normalizedEmail = Email.Trim();

        if (!MailAddress.TryCreate(normalizedEmail, out _))
            return "Email chưa đúng định dạng.";

        if (!IsGmailAddress(normalizedEmail))
            return "Chỉ chấp nhận Gmail có đuôi @gmail.com.";

        if (string.IsNullOrEmpty(Password))
            return "Vui lòng nhập mật khẩu.";

        if (Password.Length < 8)
            return "Mật khẩu phải có ít nhất 8 ký tự.";

        if (string.IsNullOrEmpty(ConfirmPassword))
            return "Vui lòng nhập lại mật khẩu.";

        return !string.Equals(Password, ConfirmPassword, StringComparison.Ordinal)
            ? "Mật khẩu xác nhận không khớp."
            : null;
    }

    private static bool IsGmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var atIndex = email.IndexOf('@');
        return atIndex > 0 &&
               atIndex == email.LastIndexOf('@') &&
               email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase);
    }

    private AuthResult ValidationFailure(string message)
    {
        Message = message;
        return new AuthResult { IsSuccess = false, Message = message };
    }

    private void ClearMessage()
    {
        if (HasMessage)
            Message = string.Empty;
    }

    private void RefreshCommand()
    {
        ((Command)RegisterCommand).ChangeCanExecute();
    }
}
