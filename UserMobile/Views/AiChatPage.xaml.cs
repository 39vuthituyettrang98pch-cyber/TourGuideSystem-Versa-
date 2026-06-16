using Microsoft.Extensions.DependencyInjection;
using UserMobile.ViewModels;

namespace UserMobile.Views;

public partial class AiChatPage : ContentPage
{
    private AiChatViewModel ViewModel => BindingContext as AiChatViewModel ?? throw new InvalidOperationException();

    public AiChatPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<AiChatViewModel>();
        ViewModel.MessageAdded += ScrollToLastMessage;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.InitializeAsync();
    }

    private async void OnChangeLanguageClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LanguageSelectionPage));
    }

    private async void OnEntryCompleted(object? sender, EventArgs e)
    {
        await ViewModel.SendMessageAsync();
    }

    private void ScrollToLastMessage()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (ViewModel.Messages.Count == 0)
                return;

            MessagesList.ScrollTo(
                ViewModel.Messages[ViewModel.Messages.Count - 1],
                position: ScrollToPosition.End,
                animate: true);
        });
    }
}
