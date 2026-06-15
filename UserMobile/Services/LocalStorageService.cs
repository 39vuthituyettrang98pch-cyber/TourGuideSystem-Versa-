using System.Text;
using Microsoft.Maui.Storage;

namespace UserMobile.Services;

public class LocalStorageService : ILocalStorageService
{
    public Task SaveAsync(string key, string value)
    {
        Preferences.Set(key, value);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        var value = Preferences.Get(key, null);
        return Task.FromResult(value);
    }

    public Task RemoveAsync(string key)
    {
        Preferences.Remove(key);
        return Task.CompletedTask;
    }
}
