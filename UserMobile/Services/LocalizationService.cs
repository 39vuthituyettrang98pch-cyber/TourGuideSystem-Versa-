using System.ComponentModel;
using Microsoft.Maui.ApplicationModel;
using System.Runtime.CompilerServices;
using UserMobile.Models;

namespace UserMobile.Services;

public sealed class LocalizationService : ILocalizationService, INotifyPropertyChanged
{
    private const string LanguageKey = "selected_language";
    private const string UiCachePrefix = "ui_i18n_mobile_";

    private readonly ILocalStorageService _storage;
    private readonly IApiService _apiService;
    private bool _loadedRemoteLanguages;
    private readonly Dictionary<string, string> _uiTranslations = new(StringComparer.OrdinalIgnoreCase);

    public static LocalizationService? Instance { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    private string _currentLanguage = "vi";
    public string CurrentLanguageCode => _currentLanguage;

    public string this[string key] => Translate(key);

    public IList<LanguageOption> SupportedLanguages { get; } = new List<LanguageOption>
    {
        new() { Code = "vi", Name = "Tiếng Việt", NativeName = "Tiếng Việt" },
        new() { Code = "en", Name = "English", NativeName = "English" },
        new() { Code = "fr", Name = "Français", NativeName = "Français" },
        new() { Code = "zh", Name = "中文", NativeName = "中文" },
        new() { Code = "ja", Name = "日本語", NativeName = "日本語" },
        new() { Code = "ko", Name = "한국어", NativeName = "한국어" }
    };

    public LocalizationService(ILocalStorageService storage, IApiService apiService)
    {
        _storage = storage;
        _apiService = apiService;
        Instance = this;
        LoadFallbackTranslations("vi");
    }

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
                    var code = NormalizeLanguageCode(language.Code);
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

    public async Task<LanguageOption?> GetSavedLanguageAsync()
    {
        if (!_loadedRemoteLanguages)
            await RefreshSupportedLanguagesAsync();

        var code = NormalizeLanguageCode(await _storage.GetAsync(LanguageKey));
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var language = SupportedLanguages.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
        if (language is null)
        {
            language = new LanguageOption
            {
                Code = code,
                Name = code.ToUpperInvariant(),
                NativeName = code.ToUpperInvariant()
            };

            SupportedLanguages.Add(language);
        }

        _currentLanguage = code;
        await RefreshUiTranslationsAsync(code);
        return language;
    }

    public async Task SetLanguageAsync(LanguageOption language)
    {
        var code = NormalizeLanguageCode(language.Code);
        if (string.IsNullOrWhiteSpace(code))
            code = "vi";

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
        await RefreshUiTranslationsAsync(code);
        NotifyLanguageChanged();
    }

    public async Task RefreshUiTranslationsAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        var code = NormalizeLanguageCode(languageCode);
        if (string.IsNullOrWhiteSpace(code))
            code = "vi";

        LoadFallbackTranslations(code);

        var cached = await _storage.GetAsync(UiCachePrefix + code);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            TryMergeSerializedDictionary(cached);
        }

        try
        {
            var response = await _apiService.GetAsync<UiTranslationResponse>(
                $"api/ui-translations?platform=mobile&lang={Uri.EscapeDataString(code)}",
                cancellationToken);

            if (response.Success && response.Data?.Translations is { Count: > 0 })
            {
                foreach (var item in response.Data.Translations)
                {
                    if (!string.IsNullOrWhiteSpace(item.Key))
                        _uiTranslations[item.Key] = item.Value ?? item.Key;
                }

                var serialized = System.Text.Json.JsonSerializer.Serialize(_uiTranslations);
                await _storage.SaveAsync(UiCachePrefix + code, serialized);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Cannot load UI translations: {exception}");
        }

        NotifyLanguageChanged();
    }

    public string Translate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        if (_uiTranslations.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;

        if (FallbackTranslations.TryGetValue("vi", out var vi) && vi.TryGetValue(key, out var viValue))
            return viValue;

        return key;
    }

    private void LoadFallbackTranslations(string languageCode)
    {
        _uiTranslations.Clear();

        foreach (var item in FallbackTranslations["vi"])
            _uiTranslations[item.Key] = item.Value;

        if (FallbackTranslations.TryGetValue(languageCode, out var selected))
        {
            foreach (var item in selected)
                _uiTranslations[item.Key] = item.Value;
        }
    }

    private void TryMergeSerializedDictionary(string serialized)
    {
        try
        {
            var map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(serialized);
            if (map is null)
                return;

            foreach (var item in map)
            {
                if (!string.IsNullOrWhiteSpace(item.Key))
                    _uiTranslations[item.Key] = item.Value;
            }
        }
        catch
        {
        }
    }

    private void NotifyLanguageChanged()
    {
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(CurrentLanguageCode));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        MainThread.BeginInvokeOnMainThread(() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }

    private static string NormalizeLanguageCode(string? value)
    {
        var code = (value ?? "").Trim().ToLowerInvariant();
        return code.Length is >= 2 and <= 10 && code.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? code
            : "";
    }

    private static readonly Dictionary<string, Dictionary<string, string>> FallbackTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vi"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["App_Title"] = "VERSA Guide",
            ["WelcomeTitle"] = "VERSA Guide",
            ["WelcomeSubtitle"] = "Khám phá điểm đến theo cách của bạn",
            ["SelectLanguage"] = "Chọn ngôn ngữ",
            ["LanguageSetupEyebrow"] = "THIẾT LẬP CÁ NHÂN",
            ["LanguageQuestion"] = "Bạn muốn nghe nội dung bằng ngôn ngữ nào?",
            ["LanguageHint"] = "Bạn có thể thay đổi lựa chọn này bất cứ lúc nào trong trang Cá nhân.",
            ["Continue"] = "Tiếp tục",
            ["Tab_Explore"] = "Khám phá",
            ["Tab_Tour"] = "Tour",
            ["Tab_Qr"] = "Quét QR",
            ["Tab_Favorites"] = "Yêu thích",
            ["Tab_Ai"] = "AI",
            ["Tab_Profile"] = "Cá nhân",
            ["Map_Title"] = "Khám phá quanh bạn",
            ["Map_Subtitle"] = "Chạm marker để xem thuyết minh",
            ["Map_Refresh"] = "Làm mới",
            ["Map_PoiListTitle"] = "Điểm tham quan",
            ["Map_PoiListSubtitle"] = "Chạm marker hoặc thẻ để mở chi tiết",
            ["Map_Empty"] = "Chưa có POI được duyệt",
            ["Map_OpenDetail"] = "Mở chi tiết",
            ["Login_Title"] = "Chào mừng trở lại",
            ["Login_Subtitle"] = "Đăng nhập để khám phá, lưu yêu thích và đồng bộ hành trình của bạn.",
            ["Login_Email"] = "EMAIL",
            ["Login_Password"] = "MẬT KHẨU",
            ["Login_Button"] = "Đăng nhập",
            ["Login_CreateAccount"] = "Tạo tài khoản mới",
            ["Login_ForgotPassword"] = "Quên mật khẩu",
            ["Login_Benefit"] = "Tài khoản giúp giữ lại địa điểm yêu thích và lịch sử tham quan trên nhiều thiết bị.",
            ["Register_Title"] = "Tạo tài khoản du khách",
            ["Register_Eyebrow"] = "BẮT ĐẦU HÀNH TRÌNH",
            ["Register_Subtitle"] = "Chỉ mất một phút để lưu địa điểm yêu thích và lịch sử khám phá của riêng bạn.",
            ["Register_FullName"] = "HỌ VÀ TÊN",
            ["Register_Email"] = "EMAIL",
            ["Register_Password"] = "MẬT KHẨU",
            ["Register_Button"] = "Đăng ký",
            ["Register_Login"] = "Đã có tài khoản? Đăng nhập",
            ["Favorites_Title"] = "Địa điểm yêu thích",
            ["Favorites_Subtitle"] = "Danh sách được lưu riêng theo tài khoản của bạn.",
            ["Favorites_OpenDetail"] = "Mở chi tiết →",
            ["Recent_Title"] = "Đã xem gần đây",
            ["Recent_Subtitle"] = "Quay lại những điểm tham quan bạn vừa mở.",
            ["Recent_Open"] = "Xem lại →",
            ["Tours_Title"] = "Tour khám phá",
            ["Tours_Subtitle"] = "Dữ liệu tour và danh mục được tải trực tiếp từ hệ thống.",
            ["Tours_Category"] = "Danh mục",
            ["Tours_ClearFilter"] = "Xóa lọc",
            ["Tours_Empty"] = "Chưa có hành trình",
            ["Tours_View"] = "Xem hành trình ›",
            ["Qr_Title"] = "Quét mã tại điểm tham quan",
            ["Qr_Subtitle"] = "Đưa mã QR vào giữa khung để mở nội dung.",
            ["Profile_Title"] = "Cá nhân",
            ["Profile_Subtitle"] = "Quản lý hành trình và thiết lập trải nghiệm của bạn.",
            ["Profile_QuickAccess"] = "TRUY CẬP NHANH",
            ["Profile_Recent"] = "Đã xem gần đây",
            ["Profile_RecentHint"] = "Mở lại điểm tham quan",
            ["Profile_Favorites"] = "Yêu thích",
            ["Profile_FavoritesHint"] = "Xem bộ sưu tập đã lưu",
            ["Profile_Account"] = "TÀI KHOẢN",
            ["Profile_Edit"] = "Sửa hồ sơ",
            ["Profile_ChangePassword"] = "Đổi mật khẩu",
            ["Profile_ForgotPassword"] = "Quên mật khẩu",
            ["Profile_Settings"] = "THIẾT LẬP",
            ["Profile_ContentLanguage"] = "Ngôn ngữ nội dung",
            ["Profile_Change"] = "Thay đổi",
            ["Profile_About"] = "Bản đồ OpenStreetMap, quét QR và thuyết minh đa ngôn ngữ trong một ứng dụng.",
            ["Profile_LoginOrRegister"] = "Đăng nhập hoặc đăng ký",
            ["Profile_Logout"] = "Đăng xuất",
            ["Ai_Title"] = "VERSA AI",
            ["Ai_Subtitle"] = "Hỏi nhanh về điểm tham quan, tour, QR và thuyết minh",
            ["Ai_CurrentLanguage"] = "Ngôn ngữ đang chọn: ",
            ["Ai_Change"] = "Đổi",
            ["Ai_SuggestPlace"] = "Gợi ý đi đâu?",
            ["Ai_Narration"] = "Nghe thuyết minh",
            ["Ai_Empty"] = "Chưa có tin nhắn",
            ["Ai_EmptyHint"] = "Nhập câu hỏi bên dưới để bắt đầu.",
            ["Ai_Send"] = "Gửi",
            ["Detail_Intro"] = "Giới thiệu",
            ["Detail_NarrationLanguage"] = "Ngôn ngữ thuyết minh",
            ["Detail_NoAudio"] = "POI này chưa có file audio hoặc video thuyết minh.",
            ["Detail_ReviewTitle"] = "Đánh giá & bình luận",
            ["Detail_SelectStars"] = "Chọn số sao",
            ["Detail_SendReview"] = "Gửi đánh giá",
            ["Detail_LoginToReview"] = "Bạn cần đăng nhập để gửi đánh giá. Nếu chưa đăng nhập, app sẽ chuyển sang trang đăng nhập.",
            ["Detail_RecentReviews"] = "Đánh giá gần đây",
            ["Detail_NoReviews"] = "Chưa có bình luận nào. Hãy là người đầu tiên đánh giá POI này.",
            ["Detail_PlayNarration"] = "Phát thuyết minh",
            ["Detail_WatchVideo"] = "Xem video thuyết minh",
            ["Common_Login"] = "Đăng nhập",
            ["Common_Logout"] = "Đăng xuất",
            ["Common_Cancel"] = "Hủy",
            ["Common_Ok"] = "OK"
        },
        ["en"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["WelcomeSubtitle"] = "Explore destinations your way", ["SelectLanguage"] = "Select language", ["LanguageSetupEyebrow"] = "PERSONAL SETUP", ["LanguageQuestion"] = "Which language do you want to use?", ["LanguageHint"] = "You can change this anytime in Profile.", ["Continue"] = "Continue",
            ["Tab_Explore"] = "Explore", ["Tab_Tour"] = "Tours", ["Tab_Qr"] = "Scan QR", ["Tab_Favorites"] = "Favorites", ["Tab_Ai"] = "AI", ["Tab_Profile"] = "Profile",
            ["Map_Title"] = "Explore around you", ["Map_Subtitle"] = "Tap a marker to view narration", ["Map_Refresh"] = "Refresh", ["Map_PoiListTitle"] = "Attractions", ["Map_PoiListSubtitle"] = "Tap a marker or card to open details", ["Map_Empty"] = "No approved POIs yet", ["Map_OpenDetail"] = "Open details",
            ["Login_Title"] = "Welcome back", ["Login_Subtitle"] = "Sign in to explore, save favorites and sync your journey.", ["Login_Email"] = "EMAIL", ["Login_Password"] = "PASSWORD", ["Login_Button"] = "Login", ["Login_CreateAccount"] = "Create new account", ["Login_ForgotPassword"] = "Forgot password", ["Login_Benefit"] = "Your account keeps favorites and visit history across devices.",
            ["Register_Title"] = "Create tourist account", ["Register_Eyebrow"] = "START YOUR JOURNEY", ["Register_Subtitle"] = "It only takes a minute to save your favorite places and history.", ["Register_FullName"] = "FULL NAME", ["Register_Email"] = "EMAIL", ["Register_Password"] = "PASSWORD", ["Register_Button"] = "Register", ["Register_Login"] = "Already have an account? Login",
            ["Favorites_Title"] = "Favorite places", ["Favorites_Subtitle"] = "Your saved places are stored with your account.", ["Favorites_OpenDetail"] = "Open details →", ["Recent_Title"] = "Recently viewed", ["Recent_Subtitle"] = "Return to attractions you opened recently.", ["Recent_Open"] = "View again →",
            ["Tours_Title"] = "Explore tours", ["Tours_Subtitle"] = "Tours and categories are loaded from the system.", ["Tours_Category"] = "Category", ["Tours_ClearFilter"] = "Clear filter", ["Tours_Empty"] = "No tours yet", ["Tours_View"] = "View itinerary ›",
            ["Qr_Title"] = "Scan QR at attraction", ["Qr_Subtitle"] = "Place the QR code in the center to open content.",
            ["Profile_Title"] = "Profile", ["Profile_Subtitle"] = "Manage your journey and experience settings.", ["Profile_QuickAccess"] = "QUICK ACCESS", ["Profile_Recent"] = "Recently viewed", ["Profile_RecentHint"] = "Open attractions again", ["Profile_Favorites"] = "Favorites", ["Profile_FavoritesHint"] = "View saved collection", ["Profile_Account"] = "ACCOUNT", ["Profile_Edit"] = "Edit profile", ["Profile_ChangePassword"] = "Change password", ["Profile_ForgotPassword"] = "Forgot password", ["Profile_Settings"] = "SETTINGS", ["Profile_ContentLanguage"] = "Content language", ["Profile_Change"] = "Change", ["Profile_About"] = "OpenStreetMap, QR scanning and multilingual narration in one app.", ["Profile_LoginOrRegister"] = "Login or register", ["Profile_Logout"] = "Logout",
            ["Ai_Subtitle"] = "Ask about attractions, tours, QR and narration", ["Ai_CurrentLanguage"] = "Selected language: ", ["Ai_Change"] = "Change", ["Ai_SuggestPlace"] = "Where should I go?", ["Ai_Narration"] = "Listen narration", ["Ai_Empty"] = "No messages yet", ["Ai_EmptyHint"] = "Enter a question below to start.", ["Ai_Send"] = "Send",
            ["Detail_Intro"] = "Introduction", ["Detail_NarrationLanguage"] = "Narration language", ["Detail_NoAudio"] = "This POI has no audio or video narration yet.", ["Detail_ReviewTitle"] = "Reviews & comments", ["Detail_SelectStars"] = "Choose stars", ["Detail_SendReview"] = "Send review", ["Detail_LoginToReview"] = "You need to log in to submit a review.", ["Detail_RecentReviews"] = "Recent reviews", ["Detail_NoReviews"] = "No comments yet. Be the first to review this POI.", ["Detail_PlayNarration"] = "Play narration", ["Detail_WatchVideo"] = "Watch video narration",
            ["Common_Login"] = "Login", ["Common_Logout"] = "Logout", ["Common_Cancel"] = "Cancel", ["Common_Ok"] = "OK"
        },
        ["fr"] = new(StringComparer.OrdinalIgnoreCase) { ["SelectLanguage"] = "Choisir la langue", ["Continue"] = "Continuer", ["Tab_Explore"] = "Explorer", ["Tab_Tour"] = "Tours", ["Tab_Qr"] = "Scanner QR", ["Tab_Favorites"] = "Favoris", ["Tab_Profile"] = "Profil", ["Login_Button"] = "Connexion", ["Profile_Logout"] = "Déconnexion" },
        ["zh"] = new(StringComparer.OrdinalIgnoreCase) { ["SelectLanguage"] = "选择语言", ["Continue"] = "继续", ["Tab_Explore"] = "探索", ["Tab_Tour"] = "路线", ["Tab_Qr"] = "扫码", ["Tab_Favorites"] = "收藏", ["Tab_Profile"] = "个人", ["Login_Button"] = "登录", ["Profile_Logout"] = "退出" },
        ["ja"] = new(StringComparer.OrdinalIgnoreCase) { ["SelectLanguage"] = "言語を選択", ["Continue"] = "続ける", ["Tab_Explore"] = "探索", ["Tab_Tour"] = "ツアー", ["Tab_Qr"] = "QR読取", ["Tab_Favorites"] = "お気に入り", ["Tab_Profile"] = "プロフィール", ["Login_Button"] = "ログイン", ["Profile_Logout"] = "ログアウト" },
        ["ko"] = new(StringComparer.OrdinalIgnoreCase) { ["SelectLanguage"] = "언어 선택", ["Continue"] = "계속", ["Tab_Explore"] = "탐색", ["Tab_Tour"] = "투어", ["Tab_Qr"] = "QR 스캔", ["Tab_Favorites"] = "즐겨찾기", ["Tab_Profile"] = "프로필", ["Login_Button"] = "로그인", ["Profile_Logout"] = "로그아웃" }
    };
}
