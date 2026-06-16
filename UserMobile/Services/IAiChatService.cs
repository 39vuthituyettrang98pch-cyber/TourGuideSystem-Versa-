using UserMobile.Models;

namespace UserMobile.Services;

public interface IAiChatService
{
    Task<ApiResponse<AiChatResponse>> AskAsync(
        string message,
        string languageCode,
        string currentScreen,
        CancellationToken cancellationToken = default);
}
