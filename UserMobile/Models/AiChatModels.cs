namespace UserMobile.Models;

public sealed class AiChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "vi";
    public string CurrentScreen { get; set; } = "App mobile";
}

public sealed class AiChatResponse
{
    public string Reply { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "vi";
    public string Source { get; set; } = "fallback";
}
