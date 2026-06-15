using System.Collections.ObjectModel;
using System.Windows.Input;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public class LanguageSelectionViewModel : BaseViewModel
{
    private readonly ILocalizationService _localizationService;
    private readonly ILocalStorageService _storageService;
    private LanguageOption? _selectedLanguage;
    private bool _isBusy;

    public ObservableCollection<LanguageOption> Languages { get; } = new();
    public ICommand SaveLanguageCommand { get; }

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            SetProperty(ref _selectedLanguage, value);
            ((Command)SaveLanguageCommand).ChangeCanExecute();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            ((Command)SaveLanguageCommand).ChangeCanExecute();
        }
    }

    public LanguageSelectionViewModel(ILocalizationService localizationService, ILocalStorageService storageService)
    {
        _localizationService = localizationService;
        _storageService = storageService;
        foreach (var language in _localizationService.SupportedLanguages)
        {
            Languages.Add(language);
        }

        SaveLanguageCommand = new Command(async () => await SaveLanguageAsync(), CanSaveLanguage);
    }

    private bool CanSaveLanguage()
    {
        return SelectedLanguage is not null && !IsBusy;
    }

    public async Task InitializeAsync()
    {
        var saved = await _localizationService.GetSavedLanguageAsync();
        if (saved != null)
        {
            SelectedLanguage = saved;
        }
        else
        {
            SelectedLanguage = Languages.FirstOrDefault();
        }
    }

    public async Task<bool> SaveLanguageAsync()
    {
        if (SelectedLanguage is null)
            return false;

        IsBusy = true;
        await _localizationService.SetLanguageAsync(SelectedLanguage);
        IsBusy = false;
        return true;
    }
}
