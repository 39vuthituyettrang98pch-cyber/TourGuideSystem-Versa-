using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class AchievementViewModel : BaseViewModel
{
    private readonly IAchievementService _achievementService;
    private AchievementSummary? _summary;
    private bool _isLoading;
    private string _message = string.Empty;

    public AchievementSummary? Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
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

    public AchievementViewModel(IAchievementService achievementService)
    {
        _achievementService = achievementService;
    }

    public async Task LoadAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            Message = string.Empty;
            Summary = await _achievementService.GetSummaryAsync();
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
