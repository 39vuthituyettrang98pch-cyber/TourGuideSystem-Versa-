using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class AiChatViewModel : BaseViewModel
{
    private readonly IAiChatService _aiChatService;
    private readonly ILocalizationService _localizationService;
    private string _currentMessage = string.Empty;
    private bool _isBusy;
    private string _message = string.Empty;
    private string _currentLanguageCode = "vi";
    private string _currentLanguageName = "Tiếng Việt";
    private bool _hasInitialized;

    public ObservableCollection<AiChatMessageViewModel> Messages { get; } = new();
    public ICommand SendMessageCommand { get; }
    public ICommand QuickQuestionCommand { get; }

    public event Action? MessageAdded;

    public AiChatViewModel(IAiChatService aiChatService, ILocalizationService localizationService)
    {
        _aiChatService = aiChatService;
        _localizationService = localizationService;
        SendMessageCommand = new Command(async () => await SendMessageAsync(), CanSendMessage);
        QuickQuestionCommand = new Command<string>(async question => await SendQuickQuestionAsync(question));
    }

    public string CurrentMessage
    {
        get => _currentMessage;
        set
        {
            SetProperty(ref _currentMessage, value);
            ((Command)SendMessageCommand).ChangeCanExecute();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            OnPropertyChanged(nameof(IsNotBusy));
            ((Command)SendMessageCommand).ChangeCanExecute();
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string CurrentLanguageCode
    {
        get => _currentLanguageCode;
        set => SetProperty(ref _currentLanguageCode, value);
    }

    public string CurrentLanguageName
    {
        get => _currentLanguageName;
        set => SetProperty(ref _currentLanguageName, value);
    }

    public async Task InitializeAsync()
    {
        var language = await _localizationService.GetSavedLanguageAsync();
        CurrentLanguageCode = string.IsNullOrWhiteSpace(language?.Code) ? "vi" : language!.Code.Trim().ToLowerInvariant();
        CurrentLanguageName = string.IsNullOrWhiteSpace(language?.NativeName)
            ? CurrentLanguageCode.ToUpperInvariant()
            : language!.NativeName;

        if (_hasInitialized)
            return;

        _hasInitialized = true;
        AddAssistantMessage(
            "Chào bạn, mình là AI hướng dẫn của VERSA Travel. Bạn có thể hỏi về điểm tham quan, tour, bản đồ, QR, ngôn ngữ hoặc thuyết minh.");
    }

    public async Task RefreshLanguageAsync()
    {
        var language = await _localizationService.GetSavedLanguageAsync();
        CurrentLanguageCode = string.IsNullOrWhiteSpace(language?.Code) ? "vi" : language!.Code.Trim().ToLowerInvariant();
        CurrentLanguageName = string.IsNullOrWhiteSpace(language?.NativeName)
            ? CurrentLanguageCode.ToUpperInvariant()
            : language!.NativeName;
    }

    private bool CanSendMessage()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(CurrentMessage);
    }

    private async Task SendQuickQuestionAsync(string? question)
    {
        if (string.IsNullOrWhiteSpace(question) || IsBusy)
            return;

        CurrentMessage = question.Trim();
        await SendMessageAsync();
    }

    public async Task SendMessageAsync()
    {
        var question = CurrentMessage.Trim();
        if (string.IsNullOrWhiteSpace(question) || IsBusy)
            return;

        CurrentMessage = string.Empty;
        AddUserMessage(question);
        IsBusy = true;
        Message = "AI đang trả lời...";

        try
        {
            await RefreshLanguageAsync();
            var response = await _aiChatService.AskAsync(
                question,
                CurrentLanguageCode,
                "AI Chat Page");

            if (response.Success && response.Data is not null && !string.IsNullOrWhiteSpace(response.Data.Reply))
            {
                AddAssistantMessage(response.Data.Reply.Trim());
                Message = response.Message;
            }
            else
            {
                AddAssistantMessage(string.IsNullOrWhiteSpace(response.Message)
                    ? "Không thể nhận câu trả lời từ máy chủ. Bạn kiểm tra AdminWeb đã chạy chưa nhé."
                    : response.Message);
                Message = response.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddUserMessage(string text)
    {
        Messages.Add(AiChatMessageViewModel.User(text));
        MessageAdded?.Invoke();
    }

    private void AddAssistantMessage(string text)
    {
        Messages.Add(AiChatMessageViewModel.Assistant(text));
        MessageAdded?.Invoke();
    }
}

public sealed class AiChatMessageViewModel
{
    public string Text { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public Color BubbleColor { get; init; } = Colors.White;
    public Color TextColor { get; init; } = Colors.Black;
    public LayoutOptions HorizontalAlignment { get; init; } = LayoutOptions.Start;

    public static AiChatMessageViewModel User(string text)
    {
        return new AiChatMessageViewModel
        {
            Text = text,
            SenderName = "Bạn",
            BubbleColor = Color.FromArgb("#5B3FE4"),
            TextColor = Colors.White,
            HorizontalAlignment = LayoutOptions.End
        };
    }

    public static AiChatMessageViewModel Assistant(string text)
    {
        return new AiChatMessageViewModel
        {
            Text = text,
            SenderName = "VERSA AI",
            BubbleColor = Colors.White,
            TextColor = Color.FromArgb("#172033"),
            HorizontalAlignment = LayoutOptions.Start
        };
    }
}
