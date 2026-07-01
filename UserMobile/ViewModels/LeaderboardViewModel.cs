using System.Collections.ObjectModel;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class LeaderboardViewModel : BaseViewModel
{
    private readonly IAchievementService _achievementService;
    private bool _isBusy;
    private string _message = string.Empty;

    public ObservableCollection<LeaderboardItem> Items { get; } = [];
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string Message
    {
        get => _message;
        set { SetProperty(ref _message, value); OnPropertyChanged(nameof(HasMessage)); }
    }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public LeaderboardViewModel(IAchievementService achievementService) => _achievementService = achievementService;

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            Message = string.Empty;
            Items.Clear();
            foreach (var item in await _achievementService.GetLeaderboardAsync())
                Items.Add(item);
            if (Items.Count == 0)
                Message = "Chưa có lượt check-in nào. Hãy là người mở đầu bảng xếp hạng!";
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        finally { IsBusy = false; }
    }
}
