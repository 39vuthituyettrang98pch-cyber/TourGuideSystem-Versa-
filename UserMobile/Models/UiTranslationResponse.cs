namespace UserMobile.Models;

public sealed class UiTranslationResponse
{
    public string Platform { get; set; } = "mobile";
    public string LanguageCode { get; set; } = "vi";
    public string Version { get; set; } = "";
    public Dictionary<string, string> Translations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
