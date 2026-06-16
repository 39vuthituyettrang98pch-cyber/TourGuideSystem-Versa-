using UserMobile.Models;

namespace UserMobile.Services;

public sealed class AiChatService : IAiChatService
{
    private readonly IApiService _apiService;

    public AiChatService(IApiService apiService)
    {
        _apiService = apiService;
    }

    public Task<ApiResponse<AiChatResponse>> AskAsync(
        string message,
        string languageCode,
        string currentScreen,
        CancellationToken cancellationToken = default)
    {
        return _apiService.PostAsync<AiChatResponse>(
            "api/ai-chat",
            new AiChatRequest
            {
                Message = message?.Trim() ?? string.Empty,
                LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "vi" : languageCode.Trim().ToLowerInvariant(),
                CurrentScreen = string.IsNullOrWhiteSpace(currentScreen) ? "App mobile" : currentScreen.Trim()
            },
            cancellationToken);
    }
}
