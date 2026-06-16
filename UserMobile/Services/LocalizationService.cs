using UserMobile.Models;

namespace UserMobile.Services;

public class LocalizationService : ILocalizationService
{
    private const string LanguageKey = "selected_language";
    private readonly ILocalStorageService _storage;
    private readonly IApiService _apiService;
    private bool _loadedRemoteLanguages;
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
    {
        ["vi"] = new()
        {
            ["WelcomeTitle"] = "Phố đi bộ",
            ["SelectLanguage"] = "Chọn ngôn ngữ",
            ["Continue"] = "Tiếp tục",
            ["Map"] = "Bản đồ",
            ["Recent"] = "Gần đây",
            ["ScanQr"] = "Quét QR",
            ["Favorites"] = "Yêu thích",
            ["Profile"] = "Cá nhân",
            ["Login"] = "Đăng nhập",
            ["Logout"] = "Đăng xuất",
            ["Settings"] = "Cài đặt",
            ["ChooseLanguageHint"] = "Chọn ngôn ngữ để tiếp tục",
            ["FavoriteRequireLogin"] = "Để xem yêu thích, vui lòng đăng nhập.",
        },
        ["en"] = new()
        {
            ["WelcomeTitle"] = "Walking Street",
            ["SelectLanguage"] = "Select language",
            ["Continue"] = "Continue",
            ["Map"] = "Map",
            ["Recent"] = "Recent",
            ["ScanQr"] = "Scan QR",
            ["Favorites"] = "Favorites",
            ["Profile"] = "Profile",
            ["Login"] = "Login",
            ["Logout"] = "Logout",
            ["Settings"] = "Settings",
            ["ChooseLanguageHint"] = "Choose your language to continue",
            ["FavoriteRequireLogin"] = "To see favorites, please log in.",
        },
        ["zh"] = new()
        {
            ["WelcomeTitle"] = "步行街",
            ["SelectLanguage"] = "选择语言",
            ["Continue"] = "继续",
            ["Map"] = "地图",
            ["Recent"] = "最近",
            ["ScanQr"] = "扫描二维码",
            ["Favorites"] = "收藏",
            ["Profile"] = "个人",
            ["Login"] = "登录",
            ["Logout"] = "登出",
            ["Settings"] = "设置",
            ["ChooseLanguageHint"] = "选择您的语言以继续",
            ["FavoriteRequireLogin"] = "要查看收藏，请登录。",
        },
        ["ja"] = new()
        {
            ["WelcomeTitle"] = "ウォーキングストリート",
            ["SelectLanguage"] = "言語を選択",
            ["Continue"] = "続ける",
            ["Map"] = "地図",
            ["Recent"] = "最近",
            ["ScanQr"] = "QRスキャン",
            ["Favorites"] = "お気に入り",
            ["Profile"] = "プロフィール",
            ["Login"] = "ログイン",
            ["Logout"] = "ログアウト",
            ["Settings"] = "設定",
            ["ChooseLanguageHint"] = "続行する言語を選択してください",
            ["FavoriteRequireLogin"] = "お気に入りを見るにはログインしてください。",
        },
        ["ko"] = new()
        {
            ["WelcomeTitle"] = "워킹 스트리트",
            ["SelectLanguage"] = "언어 선택",
            ["Continue"] = "계속",
            ["Map"] = "지도",
            ["Recent"] = "최근",
            ["ScanQr"] = "QR 스캔",
            ["Favorites"] = "즐겨찾기",
            ["Profile"] = "프로필",
            ["Login"] = "로그인",
            ["Logout"] = "로그아웃",
            ["Settings"] = "설정",
            ["ChooseLanguageHint"] = "계속할 언어를 선택하세요",
            ["FavoriteRequireLogin"] = "즐겨찾기를 보려면 로그인하세요.",
        }
    };

    private string _currentLanguage = "vi";

    public LocalizationService(ILocalStorageService storage, IApiService apiService)
    {
        _storage = storage;
        _apiService = apiService;
    }

    public IList<LanguageOption> SupportedLanguages { get; } = new List<LanguageOption>
    {
        new() { Code = "vi", Name = "Tiếng Việt", NativeName = "Tiếng Việt" },
        new() { Code = "en", Name = "English", NativeName = "English" },
        new() { Code = "zh", Name = "中文", NativeName = "中文" },
        new() { Code = "ja", Name = "日本語", NativeName = "日本語" },
        new() { Code = "ko", Name = "한국어", NativeName = "한국어" }
    };

    public async Task<IReadOnlyList<LanguageOption>> RefreshSupportedLanguagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.GetAsync<List<LanguageOption>>("api/languages", cancellationToken);
            if (response.Success && response.Data is { Count: > 0 })
            {
                SupportedLanguages.Clear();
                foreach (var language in response.Data
                    .Where(item => !string.IsNullOrWhiteSpace(item.Code))
                    .GroupBy(item => item.Code.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First()))
                {
                    var code = language.Code.Trim().ToLowerInvariant();
                    var name = string.IsNullOrWhiteSpace(language.Name) ? code.ToUpperInvariant() : language.Name.Trim();
                    var nativeName = string.IsNullOrWhiteSpace(language.NativeName) ? name : language.NativeName.Trim();

                    SupportedLanguages.Add(new LanguageOption
                    {
                        Code = code,
                        Name = name,
                        NativeName = nativeName
                    });
                }
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Cannot load remote languages: {exception}");
        }

        _loadedRemoteLanguages = true;
        return SupportedLanguages.ToList();
    }

    private async Task EnsureRemoteLanguagesLoadedAsync()
    {
        if (!_loadedRemoteLanguages)
            await RefreshSupportedLanguagesAsync();
    }

    public async Task<LanguageOption?> GetSavedLanguageAsync()
    {
        await EnsureRemoteLanguagesLoadedAsync();
        var code = await _storage.GetAsync(LanguageKey);
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return SupportedLanguages.FirstOrDefault(x => x.Code == code);
    }

    public async Task SetLanguageAsync(LanguageOption language)
    {
        var code = language.Code.Trim().ToLowerInvariant();
        if (!SupportedLanguages.Any(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            SupportedLanguages.Add(new LanguageOption
            {
                Code = code,
                Name = string.IsNullOrWhiteSpace(language.Name) ? code.ToUpperInvariant() : language.Name,
                NativeName = string.IsNullOrWhiteSpace(language.NativeName) ? language.Name : language.NativeName
            });
        }

        _currentLanguage = code;
        await _storage.SaveAsync(LanguageKey, code);
    }

    public string Translate(string key)
    {
        if (_translations.TryGetValue(_currentLanguage, out var map) && map.TryGetValue(key, out var value))
        {
            return value;
        }

        if (_translations.TryGetValue("vi", out var defaultMap) && defaultMap.TryGetValue(key, out var defaultValue))
        {
            return defaultValue;
        }

        return key;
    }
}
