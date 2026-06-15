using System.Threading.Tasks;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _isBusy;
    private string _message = string.Empty;
    private bool _hasMessage;

    public string Email
    {
        get => _email;
        set { SetProperty(ref _email, value); ((Command)LoginCommand).ChangeCanExecute(); }
    }

    public string Password
    {
        get => _password;
        set { SetProperty(ref _password, value); ((Command)LoginCommand).ChangeCanExecute(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            ((Command)LoginCommand).ChangeCanExecute();
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            SetProperty(ref _message, value);
            HasMessage = !string.IsNullOrWhiteSpace(value);
        }
    }

    public bool HasMessage
    {
        get => _hasMessage;
        set => SetProperty(ref _hasMessage, value);
    }

    public ICommand LoginCommand { get; }
    public event EventHandler? LoginSucceeded;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        LoginCommand = new Command(async () => await LoginAsync(), CanExecuteLogin);
    }

    public async Task<AuthResult> LoginAsync()
    {
        try
        {
            IsBusy = true;
            Message = string.Empty;
            var result = await _authService.LoginAsync(Email, Password);
            Message = result.Message;
            if (result.IsSuccess)
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExecuteLogin()
    {
        return !IsBusy &&
               !string.IsNullOrWhiteSpace(Email) &&
               !string.IsNullOrWhiteSpace(Password);
    }
}
