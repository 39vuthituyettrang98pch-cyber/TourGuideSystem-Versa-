using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AdminWeb.Controllers;

[ApiController]
[Route("api/ui-translations")]
public sealed class UiTranslationApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public UiTranslationApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ApiResponse<UiTranslationDto>> Get(
        [FromQuery] string? platform = "mobile",
        [FromQuery] string? lang = "vi",
        CancellationToken cancellationToken = default)
    {
        var normalizedPlatform = NormalizePlatform(platform);
        var languageCode = NormalizeLanguageCode(lang);

        await EnsureTableAndSeedAsync(cancellationToken);

        var translations = BuildFallbackMap(languageCode);
        var dbTranslations = await ReadTranslationsAsync(normalizedPlatform, languageCode, cancellationToken);
        var version = await ReadVersionAsync(normalizedPlatform, languageCode, cancellationToken);

        foreach (var item in dbTranslations)
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
                translations[item.Key] = item.Value;
        }

        return ApiResponse<UiTranslationDto>.Ok(new UiTranslationDto
        {
            Platform = normalizedPlatform,
            LanguageCode = languageCode,
            Version = version,
            Translations = translations
        });
    }

    [HttpGet("keys")]
    public ApiResponse<IReadOnlyList<string>> Keys()
    {
        return ApiResponse<IReadOnlyList<string>>.Ok(DefaultTranslations["vi"].Keys.OrderBy(key => key).ToList());
    }

    private async Task<Dictionary<string, string>> ReadTranslationsAsync(
        string platform,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT translation_key, translation_value
                FROM ui_translations
                WHERE platform = $platform AND language_code = $languageCode;";

            var platformParam = command.CreateParameter();
            platformParam.ParameterName = "$platform";
            platformParam.Value = platform;
            command.Parameters.Add(platformParam);

            var languageParam = command.CreateParameter();
            languageParam.ParameterName = "$languageCode";
            languageParam.Value = languageCode;
            command.Parameters.Add(languageParam);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader.GetString(0);
                var value = reader.GetString(1);
                result[key] = value;
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return result;
    }

    private async Task<string> ReadVersionAsync(
        string platform,
        string languageCode,
        CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT MAX(updated_at)
                FROM ui_translations
                WHERE platform = $platform AND language_code = $languageCode;";

            AddParameter(command, "$platform", platform);
            AddParameter(command, "$languageCode", languageCode);

            var value = await command.ExecuteScalarAsync(cancellationToken);
            var latestUpdatedAt = value?.ToString();

            if (DateTimeOffset.TryParse(latestUpdatedAt, out var parsedDate))
            {
                return $"{platform}-{languageCode}-{parsedDate.UtcDateTime:yyyyMMddHHmmss}";
            }

            return $"{platform}-{languageCode}-default-v1";
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private async Task EnsureTableAndSeedAsync(CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _context.Database.GetDbConnection();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ui_translations (
                        id INTEGER NOT NULL CONSTRAINT PK_ui_translations PRIMARY KEY AUTOINCREMENT,
                        platform TEXT NOT NULL,
                        language_code TEXT NOT NULL,
                        translation_key TEXT NOT NULL,
                        translation_value TEXT NOT NULL,
                        updated_at TEXT NOT NULL,
                        CONSTRAINT UX_ui_translations_platform_language_key UNIQUE (platform, language_code, translation_key)
                    );";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var language in DefaultTranslations)
            {
                foreach (var item in language.Value)
                {
                    await using var insert = connection.CreateCommand();
                    insert.CommandText = @"
                        INSERT OR IGNORE INTO ui_translations
                            (platform, language_code, translation_key, translation_value, updated_at)
                        VALUES
                            ($platform, $languageCode, $key, $value, $updatedAt);";

                    AddParameter(insert, "$platform", "mobile");
                    AddParameter(insert, "$languageCode", language.Key);
                    AddParameter(insert, "$key", item.Key);
                    AddParameter(insert, "$value", item.Value);
                    AddParameter(insert, "$updatedAt", DateTime.UtcNow.ToString("O"));

                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static Dictionary<string, string> BuildFallbackMap(string languageCode)
    {
        var map = new Dictionary<string, string>(DefaultTranslations["vi"], StringComparer.OrdinalIgnoreCase);

        if (DefaultTranslations.TryGetValue(languageCode, out var selected))
        {
            foreach (var item in selected)
                map[item.Key] = item.Value;
        }

        return map;
    }

    private static string NormalizePlatform(string? value)
    {
        var platform = (value ?? "mobile").Trim().ToLowerInvariant();
        return platform is "mobile" or "web" ? platform : "mobile";
    }

    private static string NormalizeLanguageCode(string? value)
    {
        var code = (value ?? "vi").Trim().ToLowerInvariant();
        return code.Length is >= 2 and <= 10 && code.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? code
            : "vi";
    }

    private static readonly Dictionary<string, Dictionary<string, string>> DefaultTranslations = new(StringComparer.OrdinalIgnoreCase)
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
            ["App_Title"] = "VERSA Guide",
            ["WelcomeTitle"] = "VERSA Guide",
            ["WelcomeSubtitle"] = "Explore destinations your way",
            ["SelectLanguage"] = "Select language",
            ["LanguageSetupEyebrow"] = "PERSONAL SETUP",
            ["LanguageQuestion"] = "Which language do you want to use?",
            ["LanguageHint"] = "You can change this anytime in Profile.",
            ["Continue"] = "Continue",
            ["Tab_Explore"] = "Explore",
            ["Tab_Tour"] = "Tours",
            ["Tab_Qr"] = "Scan QR",
            ["Tab_Favorites"] = "Favorites",
            ["Tab_Ai"] = "AI",
            ["Tab_Profile"] = "Profile",
            ["Map_Title"] = "Explore around you",
            ["Map_Subtitle"] = "Tap a marker to view narration",
            ["Map_Refresh"] = "Refresh",
            ["Map_PoiListTitle"] = "Attractions",
            ["Map_PoiListSubtitle"] = "Tap a marker or card to open details",
            ["Map_Empty"] = "No approved POIs yet",
            ["Map_OpenDetail"] = "Open details",
            ["Login_Title"] = "Welcome back",
            ["Login_Subtitle"] = "Sign in to explore, save favorites and sync your journey.",
            ["Login_Email"] = "EMAIL",
            ["Login_Password"] = "PASSWORD",
            ["Login_Button"] = "Login",
            ["Login_CreateAccount"] = "Create new account",
            ["Login_ForgotPassword"] = "Forgot password",
            ["Login_Benefit"] = "Your account keeps favorites and visit history across devices.",
            ["Register_Title"] = "Create tourist account",
            ["Register_Eyebrow"] = "START YOUR JOURNEY",
            ["Register_Subtitle"] = "It only takes a minute to save your favorite places and history.",
            ["Register_FullName"] = "FULL NAME",
            ["Register_Email"] = "EMAIL",
            ["Register_Password"] = "PASSWORD",
            ["Register_Button"] = "Register",
            ["Register_Login"] = "Already have an account? Login",
            ["Favorites_Title"] = "Favorite places",
            ["Favorites_Subtitle"] = "Your saved places are stored with your account.",
            ["Favorites_OpenDetail"] = "Open details →",
            ["Recent_Title"] = "Recently viewed",
            ["Recent_Subtitle"] = "Return to attractions you opened recently.",
            ["Recent_Open"] = "View again →",
            ["Tours_Title"] = "Explore tours",
            ["Tours_Subtitle"] = "Tours and categories are loaded from the system.",
            ["Tours_Category"] = "Category",
            ["Tours_ClearFilter"] = "Clear filter",
            ["Tours_Empty"] = "No tours yet",
            ["Tours_View"] = "View itinerary ›",
            ["Qr_Title"] = "Scan QR at attraction",
            ["Qr_Subtitle"] = "Place the QR code in the center to open content.",
            ["Profile_Title"] = "Profile",
            ["Profile_Subtitle"] = "Manage your journey and experience settings.",
            ["Profile_QuickAccess"] = "QUICK ACCESS",
            ["Profile_Recent"] = "Recently viewed",
            ["Profile_RecentHint"] = "Open attractions again",
            ["Profile_Favorites"] = "Favorites",
            ["Profile_FavoritesHint"] = "View saved collection",
            ["Profile_Account"] = "ACCOUNT",
            ["Profile_Edit"] = "Edit profile",
            ["Profile_ChangePassword"] = "Change password",
            ["Profile_ForgotPassword"] = "Forgot password",
            ["Profile_Settings"] = "SETTINGS",
            ["Profile_ContentLanguage"] = "Content language",
            ["Profile_Change"] = "Change",
            ["Profile_About"] = "OpenStreetMap, QR scanning and multilingual narration in one app.",
            ["Profile_LoginOrRegister"] = "Login or register",
            ["Profile_Logout"] = "Logout",
            ["Ai_Title"] = "VERSA AI",
            ["Ai_Subtitle"] = "Ask about attractions, tours, QR and narration",
            ["Ai_CurrentLanguage"] = "Selected language: ",
            ["Ai_Change"] = "Change",
            ["Ai_SuggestPlace"] = "Where should I go?",
            ["Ai_Narration"] = "Listen narration",
            ["Ai_Empty"] = "No messages yet",
            ["Ai_EmptyHint"] = "Enter a question below to start.",
            ["Ai_Send"] = "Send",
            ["Detail_Intro"] = "Introduction",
            ["Detail_NarrationLanguage"] = "Narration language",
            ["Detail_NoAudio"] = "This POI has no audio or video narration yet.",
            ["Detail_ReviewTitle"] = "Reviews & comments",
            ["Detail_SelectStars"] = "Choose stars",
            ["Detail_SendReview"] = "Send review",
            ["Detail_LoginToReview"] = "You need to log in to submit a review.",
            ["Detail_RecentReviews"] = "Recent reviews",
            ["Detail_NoReviews"] = "No comments yet. Be the first to review this POI.",
            ["Detail_PlayNarration"] = "Play narration",
            ["Detail_WatchVideo"] = "Watch video narration",
            ["Common_Login"] = "Login",
            ["Common_Logout"] = "Logout",
            ["Common_Cancel"] = "Cancel",
            ["Common_Ok"] = "OK"
        },
        ["fr"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tab_Explore"] = "Explorer", ["Tab_Tour"] = "Tours", ["Tab_Qr"] = "Scanner QR", ["Tab_Favorites"] = "Favoris", ["Tab_Profile"] = "Profil",
            ["SelectLanguage"] = "Choisir la langue", ["Continue"] = "Continuer", ["Login_Button"] = "Connexion", ["Profile_Logout"] = "Déconnexion"
        },
        ["zh"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tab_Explore"] = "探索", ["Tab_Tour"] = "路线", ["Tab_Qr"] = "扫码", ["Tab_Favorites"] = "收藏", ["Tab_Profile"] = "个人",
            ["SelectLanguage"] = "选择语言", ["Continue"] = "继续", ["Login_Button"] = "登录", ["Profile_Logout"] = "退出"
        },
        ["ja"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tab_Explore"] = "探索", ["Tab_Tour"] = "ツアー", ["Tab_Qr"] = "QR読取", ["Tab_Favorites"] = "お気に入り", ["Tab_Profile"] = "プロフィール",
            ["SelectLanguage"] = "言語を選択", ["Continue"] = "続ける", ["Login_Button"] = "ログイン", ["Profile_Logout"] = "ログアウト"
        },
        ["ko"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tab_Explore"] = "탐색", ["Tab_Tour"] = "투어", ["Tab_Qr"] = "QR 스캔", ["Tab_Favorites"] = "즐겨찾기", ["Tab_Profile"] = "프로필",
            ["SelectLanguage"] = "언어 선택", ["Continue"] = "계속", ["Login_Button"] = "로그인", ["Profile_Logout"] = "로그아웃"
        }
    };

    public sealed class UiTranslationDto
    {
        public string Platform { get; init; } = "mobile";
        public string LanguageCode { get; init; } = "vi";
        public string Version { get; init; } = "";
        public IReadOnlyDictionary<string, string> Translations { get; init; } = new Dictionary<string, string>();
    }
}
