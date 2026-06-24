using UserMobile.Models;

namespace UserMobile.Services;

public interface ILocalizationService
{
    event EventHandler? LanguageChanged;

    IList<LanguageOption> SupportedLanguages { get; }
    string CurrentLanguageCode { get; }

    Task<IReadOnlyList<LanguageOption>> RefreshSupportedLanguagesAsync(CancellationToken cancellationToken = default);
    Task<LanguageOption?> GetSavedLanguageAsync();
    Task SetLanguageAsync(LanguageOption language);
    Task RefreshUiTranslationsAsync(string languageCode, CancellationToken cancellationToken = default);
    string Translate(string key);
}
