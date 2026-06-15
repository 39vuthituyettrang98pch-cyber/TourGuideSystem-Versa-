using UserMobile.Models;

namespace UserMobile.Services;

public interface ILocalizationService
{
    IList<LanguageOption> SupportedLanguages { get; }
    Task<LanguageOption?> GetSavedLanguageAsync();
    Task SetLanguageAsync(LanguageOption language);
    string Translate(string key);
}
