using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Services;

public sealed class DemoDataSeeder
{
    private const string SeedKey = "DemoData:SaigonFullV1";
    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        AppDbContext context,
        PasswordService passwordService,
        ILogger<DemoDataSeeder> logger)
    {
        _context = context;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await EnsureFiveLanguagePoiTranslationsAsync(now, cancellationToken);

        var alreadySeeded = await _context.SystemSettings
            .AnyAsync(item => item.SettingKey == SeedKey, cancellationToken);

        if (alreadySeeded)
            return;

        _logger.LogInformation("Seeding VERSA Saigon demo data...");

        var adminUserId = await _context.Users
            .Where(item => item.Username == "admin" || item.Email == "admin@local")
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var categoryIds = await SeedCategoriesAsync(cancellationToken);
        var touristIds = await SeedTouristsAsync(cancellationToken);
        var poiIds = await SeedPoisAsync(categoryIds, adminUserId, now, cancellationToken);
        await SeedToursAsync(poiIds, cancellationToken);
        await SeedInteractionsAsync(poiIds, touristIds, now, cancellationToken);
        await EnsureFiveLanguagePoiTranslationsAsync(now, cancellationToken);

        _context.SystemSettings.Add(new SystemSetting
        {
            SettingKey = SeedKey,
            SettingValue = DateTime.UtcNow.ToString("O"),
            Description = "Đánh dấu đã thêm bộ dữ liệu demo Sài Gòn gồm POI, tour, du khách, đánh giá, lịch sử phát và check-in."
        });

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("VERSA Saigon demo data seeded successfully.");
    }

    private async Task<Dictionary<string, int>> SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var categories = new[]
        {
            new DemoCategory("history", "Lịch sử", "History", "/uploads/demo/icons/history.svg"),
            new DemoCategory("architecture", "Kiến trúc", "Architecture", "/uploads/demo/icons/architecture.svg"),
            new DemoCategory("culture", "Văn hóa nghệ thuật", "Arts & Culture", "/uploads/demo/icons/culture.svg"),
            new DemoCategory("market", "Chợ & ẩm thực", "Markets & Food", "/uploads/demo/icons/market.svg"),
            new DemoCategory("spiritual", "Tâm linh", "Spiritual", "/uploads/demo/icons/spiritual.svg"),
            new DemoCategory("park", "Công viên & gia đình", "Parks & Family", "/uploads/demo/icons/park.svg"),
            new DemoCategory("modern", "Sài Gòn hiện đại", "Modern Saigon", "/uploads/demo/icons/modern.svg"),
            new DemoCategory("river", "Sông nước", "Riverside", "/uploads/demo/icons/river.svg")
        };

        foreach (var item in categories)
        {
            var existing = await _context.Categories
                .Include(category => category.Translations)
                .FirstOrDefaultAsync(category =>
                    category.Translations.Any(translation =>
                        translation.LanguageCode == "vi" && translation.Name == item.ViName),
                    cancellationToken);

            if (existing == null)
            {
                existing = new Category
                {
                    IconUrl = item.IconUrl,
                    Status = "active",
                    Translations =
                    [
                        new CategoryTranslation
                        {
                            LanguageCode = "vi",
                            Name = item.ViName
                        },
                        new CategoryTranslation
                        {
                            LanguageCode = "en",
                            Name = item.EnName
                        }
                    ]
                };

                _context.Categories.Add(existing);
                await _context.SaveChangesAsync(cancellationToken);
            }

            result[item.Key] = existing.Id;
        }

        return result;
    }

    private async Task<List<int>> SeedTouristsAsync(CancellationToken cancellationToken)
    {
        var tourists = new[]
        {
            new DemoTourist("tourist@local", "Du khách Demo", "tourist123"),
            new DemoTourist("ngocanh.demo@versa.local", "Ngọc Anh", "123456"),
            new DemoTourist("minhkhoi.demo@versa.local", "Minh Khôi", "123456"),
            new DemoTourist("lanphuong.demo@versa.local", "Lan Phương", "123456"),
            new DemoTourist("jaeho.demo@versa.local", "Jae Ho", "123456")
        };

        var ids = new List<int>();

        foreach (var item in tourists)
        {
            var email = item.Email.ToLowerInvariant();
            var tourist = await _context.Tourists
                .FirstOrDefaultAsync(tourist => tourist.Email == email, cancellationToken);

            if (tourist == null)
            {
                tourist = new Tourist
                {
                    Email = email,
                    FullName = item.FullName,
                    PasswordHash = _passwordService.Hash(item.Password),
                    AuthProvider = "local",
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                };

                _context.Tourists.Add(tourist);
                await _context.SaveChangesAsync(cancellationToken);
            }

            ids.Add(tourist.Id);
        }

        return ids;
    }

    private async Task<Dictionary<string, int>> SeedPoisAsync(
        Dictionary<string, int> categoryIds,
        int? adminUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pois = BuildPois();
        var index = 0;

        foreach (var item in pois)
        {
            index++;
            var existingId = await _context.PoiTranslations
                .Where(translation => translation.LanguageCode == "vi" && translation.Name == item.Name)
                .Select(translation => (int?)translation.PoiId)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingId.HasValue)
            {
                result[item.Slug] = existingId.Value;
                continue;
            }

            var poi = new Poi
            {
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                Radius = item.Radius,
                QrCodeToken = Guid.NewGuid().ToString("N"),
                Status = "Approved",
                AdminNote = "Dữ liệu demo Sài Gòn đã được duyệt sẵn.",
                CreatedBy = adminUserId,
                CoverImageUrl = $"/uploads/demo/poi-covers/{item.Slug}.svg",
                CreatedAt = now.AddDays(-index),
                Translations =
                [
                    new PoiTranslation
                    {
                        LanguageCode = "vi",
                        Name = item.Name,
                        ShortDescription = item.ShortDescription,
                        FullDescription = item.FullDescription,
                        TtsScript = item.FullDescription,
                        AudioUrl = null,
                        VideoUrl = null,
                        AudioDuration = 0,
                        UpdatedAt = now.AddDays(-index)
                    },
                    new PoiTranslation
                    {
                        LanguageCode = "en",
                        Name = item.EnName,
                        ShortDescription = item.EnShortDescription,
                        FullDescription = item.EnFullDescription,
                        TtsScript = item.EnFullDescription,
                        AudioUrl = null,
                        VideoUrl = null,
                        AudioDuration = 0,
                        UpdatedAt = now.AddDays(-index)
                    }
                ],
                MediaAssets =
                [
                    new MediaAsset
                    {
                        MediaType = "image",
                        MediaUrl = $"/uploads/demo/poi-covers/{item.Slug}.svg",
                        SortOrder = 1
                    }
                ],
                Beacons =
                [
                    new Beacon
                    {
                        Uuid = "fda50693-a4e2-4fb1-afcf-c6eb07647825",
                        Major = 100,
                        Minor = 1000 + index,
                        PlacementNote = $"Demo beacon đặt gần khu vực chính của {item.Name}."
                    }
                ]
            };

            foreach (var categoryKey in item.CategoryKeys)
            {
                if (categoryIds.TryGetValue(categoryKey, out var categoryId))
                {
                    poi.PoiCategories.Add(new PoiCategory
                    {
                        CategoryId = categoryId
                    });
                }
            }

            _context.Pois.Add(poi);
            await _context.SaveChangesAsync(cancellationToken);
            result[item.Slug] = poi.Id;
        }

        return result;
    }

    private async Task SeedToursAsync(Dictionary<string, int> poiIds, CancellationToken cancellationToken)
    {
        var tours = new[]
        {
            new DemoTour(
                "tour-historic-core",
                "Sài Gòn lịch sử trung tâm",
                "Hành trình dành cho người muốn hiểu lớp ký ức đô thị: dinh thự, bảo tàng, nhà thờ, bưu điện và đường sách.",
                "Historic Saigon Core",
                "A compact route through palaces, museums, colonial landmarks and book street in central Saigon.",
                180,
                ["dinh-doc-lap", "bao-tang-chung-tich-chien-tranh", "nha-tho-duc-ba", "buu-dien-trung-tam", "duong-sach-nguyen-van-binh"]),
            new DemoTour(
                "tour-colonial-walk",
                "Kiến trúc Pháp & phố đi bộ",
                "Tuyến tham quan nhẹ nhàng qua các công trình biểu tượng quanh Đồng Khởi, Nguyễn Huệ và khu ven sông.",
                "Colonial Architecture & Walking Street",
                "A walking-friendly route around Dong Khoi, Nguyen Hue and the riverside skyline.",
                150,
                ["nha-hat-thanh-pho", "pho-di-bo-nguyen-hue", "bitexco-financial-tower", "bao-tang-my-thuat", "ben-nha-rong"]),
            new DemoTour(
                "tour-cholon-heritage",
                "Tâm linh & di sản Chợ Lớn",
                "Gợi ý khám phá chiều sâu văn hóa qua chùa cổ, chợ truyền thống và không khí sinh hoạt cộng đồng.",
                "Spiritual & Chinatown Heritage",
                "A heritage route through temples, old markets and community life.",
                210,
                ["chua-ngoc-hoang", "chua-giac-lam", "cho-binh-tay", "cho-ben-thanh"]),
            new DemoTour(
                "tour-modern-night",
                "Sài Gòn hiện đại về đêm",
                "Lộ trình cho buổi chiều tối: skyline mới, cầu vượt sông, phố đi bộ và khu phố giải trí.",
                "Modern Saigon by Night",
                "An evening route through skyline viewpoints, river crossings and nightlife streets.",
                180,
                ["landmark-81", "cau-ba-son", "bitexco-financial-tower", "pho-di-bo-nguyen-hue", "pho-di-bo-bui-vien"]),
            new DemoTour(
                "tour-family-green",
                "Gia đình xanh & bảo tàng",
                "Tuyến nhẹ cho gia đình: công viên, thảo cầm viên, bảo tàng lịch sử và đường sách.",
                "Family Green & Museum Route",
                "A family-friendly route covering parks, zoo, history museum and book street.",
                160,
                ["cong-vien-tao-dan", "thao-cam-vien-sai-gon", "bao-tang-lich-su-tphcm", "duong-sach-nguyen-van-binh"])
        };

        foreach (var item in tours)
        {
            var exists = await _context.TourTranslations
                .AnyAsync(translation => translation.LanguageCode == "vi" && translation.Title == item.Title, cancellationToken);

            if (exists)
                continue;

            var tour = new Tour
            {
                EstimatedTime = item.DurationMinutes,
                Status = "active",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                Translations =
                [
                    new TourTranslation
                    {
                        LanguageCode = "vi",
                        Title = item.Title,
                        Description = item.Description
                    },
                    new TourTranslation
                    {
                        LanguageCode = "en",
                        Title = item.EnTitle,
                        Description = item.EnDescription
                    }
                ]
            };

            var sequence = 1;
            foreach (var slug in item.PoiSlugs)
            {
                if (!poiIds.TryGetValue(slug, out var poiId))
                    continue;

                tour.TourPois.Add(new TourPoi
                {
                    PoiId = poiId,
                    SequenceOrder = sequence++
                });
            }

            _context.Tours.Add(tour);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedInteractionsAsync(
        Dictionary<string, int> poiIds,
        List<int> touristIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (touristIds.Count == 0 || poiIds.Count == 0)
            return;

        var reviewTemplates = new[]
        {
            "Địa điểm rất đáng ghé, phần thuyết minh dễ hiểu và vị trí trên bản đồ khá chính xác.",
            "Không gian đẹp, phù hợp để chụp ảnh và tìm hiểu thêm về lịch sử Sài Gòn.",
            "Trải nghiệm ổn, nên đi vào buổi sáng hoặc chiều mát để dễ tham quan hơn.",
            "Mình thích cách app gợi ý tuyến đường và lưu lại điểm đã khám phá.",
            "Nội dung mô tả ngắn gọn, đủ thông tin cho khách lần đầu đến TP.HCM."
        };

        var orderedPois = poiIds.Values.ToList();
        var random = new Random(20260616);

        foreach (var poiId in orderedPois)
        {
            for (var i = 0; i < Math.Min(3, touristIds.Count); i++)
            {
                var touristId = touristIds[(i + poiId) % touristIds.Count];
                var existingReview = await _context.PoiReviews
                    .AnyAsync(item => item.PoiId == poiId && item.TouristId == touristId, cancellationToken);

                if (!existingReview)
                {
                    _context.PoiReviews.Add(new PoiReview
                    {
                        PoiId = poiId,
                        TouristId = touristId,
                        Rating = 4 + ((poiId + i) % 2),
                        Comment = reviewTemplates[(poiId + i) % reviewTemplates.Length],
                        CreatedAt = now.AddDays(-random.Next(1, 25)).AddHours(-random.Next(0, 12))
                    });
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var touristId in touristIds.Take(4))
        {
            foreach (var poiId in orderedPois.Where((_, index) => (index + touristId) % 4 == 0).Take(6))
            {
                var favoriteExists = await _context.TouristFavorites
                    .AnyAsync(item => item.TouristId == touristId && item.TargetType == "POI" && item.TargetId == poiId, cancellationToken);

                if (!favoriteExists)
                {
                    _context.TouristFavorites.Add(new TouristFavorite
                    {
                        TouristId = touristId,
                        TargetType = "POI",
                        TargetId = poiId,
                        CreatedAt = now.AddDays(-random.Next(1, 20))
                    });
                }

                var discoveryExists = await _context.TouristPoiDiscoveries
                    .AnyAsync(item => item.TouristId == touristId && item.PoiId == poiId, cancellationToken);

                if (!discoveryExists)
                {
                    _context.TouristPoiDiscoveries.Add(new TouristPoiDiscovery
                    {
                        TouristId = touristId,
                        PoiId = poiId,
                        DiscoveryMethod = random.Next(0, 2) == 0 ? "GPS" : "QR",
                        PointsAwarded = 10,
                        VisitorLatitude = null,
                        VisitorLongitude = null,
                        DiscoveredAt = now.AddDays(-random.Next(1, 18)).AddHours(-random.Next(0, 10))
                    });
                }
            }
        }

        var hasDemoLogs = await _context.VisitorPlaybackLogs
            .AnyAsync(item => item.DeviceId.StartsWith("demo-saigon-"), cancellationToken);

        if (!hasDemoLogs)
        {
            var languages = new[] { "vi", "en", "ja", "ko" };
            var triggers = new[] { "GPS", "QR", "WebMap", "MobileDetail" };
            var logIndex = 0;

            foreach (var poiId in orderedPois)
            {
                var logCount = random.Next(4, 11);
                for (var i = 0; i < logCount; i++)
                {
                    var touristId = random.NextDouble() > 0.25
                        ? touristIds[random.Next(touristIds.Count)]
                        : (int?)null;

                    _context.VisitorPlaybackLogs.Add(new VisitorPlaybackLog
                    {
                        TouristId = touristId,
                        DeviceId = $"demo-saigon-{(touristId.HasValue ? touristId.Value : 0)}-{logIndex++}",
                        PoiId = poiId,
                        LanguageCode = languages[random.Next(languages.Length)],
                        TriggerType = triggers[random.Next(triggers.Length)],
                        VisitorLatitude = null,
                        VisitorLongitude = null,
                        ListenDuration = random.Next(35, 260),
                        CreatedAt = now.AddDays(-random.Next(0, 7)).AddMinutes(-random.Next(0, 1440))
                    });
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureFiveLanguagePoiTranslationsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var demoLanguages = GetDemoLanguages();

        foreach (var item in demoLanguages)
        {
            var language = await _context.SupportedLanguages
                .FirstOrDefaultAsync(row => row.LanguageCode == item.Code, cancellationToken);

            if (language == null)
            {
                _context.SupportedLanguages.Add(new SupportedLanguage
                {
                    LanguageCode = item.Code,
                    LanguageName = item.Name,
                    EdgeTtsVoice = item.EdgeTtsVoice,
                    IsActive = true
                });
            }
            else
            {
                language.LanguageName = item.Name;
                language.EdgeTtsVoice = item.EdgeTtsVoice;
                language.IsActive = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var pois = await _context.Pois
            .Include(poi => poi.Translations)
            .ToListAsync(cancellationToken);

        foreach (var poi in pois)
        {
            var source = poi.Translations.FirstOrDefault(item => item.LanguageCode == "en")
                ?? poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")
                ?? poi.Translations.FirstOrDefault();

            if (source == null)
                continue;

            foreach (var language in demoLanguages)
            {
                var exists = poi.Translations.Any(item =>
                    string.Equals(item.LanguageCode, language.Code, StringComparison.OrdinalIgnoreCase));

                if (exists)
                    continue;

                poi.Translations.Add(CreateLocalizedPoiTranslation(language, source, now));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<DemoLanguage> GetDemoLanguages() =>
    [
        new DemoLanguage("vi", "Tiếng Việt", "vi-VN-HoaiMyNeural"),
        new DemoLanguage("en", "English", "en-US-AriaNeural"),
        new DemoLanguage("zh", "中文", "zh-CN-XiaoxiaoNeural"),
        new DemoLanguage("ja", "日本語", "ja-JP-NanamiNeural"),
        new DemoLanguage("ko", "한국어", "ko-KR-SunHiNeural")
    ];

    private static PoiTranslation CreateLocalizedPoiTranslation(DemoLanguage language, PoiTranslation source, DateTime now)
    {
        var name = string.IsNullOrWhiteSpace(source.Name) ? "POI" : source.Name;

        var shortDescription = language.Code switch
        {
            "en" => $"Audio guide for {name} in Ho Chi Minh City.",
            "zh" => $"{name} 的胡志明市语音导览。",
            "ja" => $"{name} のホーチミン市観光音声ガイドです。",
            "ko" => $"{name}의 호치민시 관광 음성 안내입니다.",
            _ => source.ShortDescription ?? $"Thuyết minh tham quan cho {name}."
        };

        var fullDescription = language.Code switch
        {
            "en" => !string.IsNullOrWhiteSpace(source.FullDescription)
                ? source.FullDescription!
                : $"This is {name}, a point of interest in Ho Chi Minh City. This audio guide introduces the location, its visitor experience, and useful tips for your trip.",
            "zh" => $"这里是 {name}，胡志明市的一个旅游景点。本语音导览将为您介绍这个地点、参观体验以及旅行时需要注意的提示。请根据地图位置前往，并尊重周围环境与当地文化。",
            "ja" => $"ここは {name}、ホーチミン市の観光スポットです。この音声ガイドでは、場所の特徴、見学のポイント、旅行中に役立つヒントを紹介します。地図を確認しながら移動し、周囲の環境と現地の文化を尊重してください。",
            "ko" => $"이곳은 {name}, 호치민시의 관광 명소입니다. 이 음성 안내는 장소의 특징, 관람 포인트, 여행에 도움이 되는 정보를 소개합니다. 지도를 확인하며 이동하고 주변 환경과 현지 문화를 존중해 주세요.",
            _ => source.FullDescription ?? source.ShortDescription ?? $"Đây là {name}, một điểm tham quan tại Thành phố Hồ Chí Minh."
        };

        return new PoiTranslation
        {
            LanguageCode = language.Code,
            Name = name,
            ShortDescription = shortDescription,
            FullDescription = fullDescription,
            TtsScript = fullDescription,
            AudioUrl = null,
            VideoUrl = null,
            AudioDuration = 0,
            UpdatedAt = now
        };
    }

    private static IReadOnlyList<DemoPoi> BuildPois() =>
    [
        new DemoPoi(
            "dinh-doc-lap",
            "Dinh Độc Lập",
            "Independence Palace",
            10.777000m,
            106.695300m,
            70,
            ["history", "architecture"],
            "Công trình lịch sử gắn với nhiều dấu mốc quan trọng của Sài Gòn và Việt Nam hiện đại.",
            "Dinh Độc Lập là một biểu tượng lịch sử nằm giữa trung tâm Quận 1. Công trình gây ấn tượng bởi kiến trúc hiện đại của thập niên 1960, các phòng họp, phòng tiếp khách và hệ thống hầm bên dưới. Khi tham quan, du khách có thể hình dung rõ hơn về bối cảnh chính trị, ngoại giao và đời sống đô thị Sài Gòn trong thế kỷ 20. Đây là điểm mở đầu phù hợp cho tuyến khám phá lịch sử trung tâm thành phố.",
            "A historic landmark associated with major moments of modern Saigon and Vietnam.",
            "Independence Palace is a central historic landmark in District 1. Its 1960s architecture, reception rooms, meeting halls and underground areas help visitors imagine the political and urban context of Saigon in the twentieth century. It is a strong starting point for a historic city route."),
        new DemoPoi(
            "nha-tho-duc-ba",
            "Nhà thờ Đức Bà Sài Gòn",
            "Notre Dame Cathedral of Saigon",
            10.779800m,
            106.699000m,
            55,
            ["architecture", "spiritual"],
            "Biểu tượng kiến trúc Công giáo nổi bật tại khu trung tâm lịch sử của thành phố.",
            "Nhà thờ Đức Bà Sài Gòn nổi bật với mặt đứng gạch đỏ, hai tháp chuông cao và vị trí ngay cạnh Bưu điện Trung tâm. Không gian xung quanh thường là điểm dừng chân để ngắm kiến trúc, chụp ảnh và cảm nhận nhịp sống khu Đồng Khởi. Công trình thể hiện dấu ấn giao thoa giữa lịch sử đô thị, tôn giáo và kiến trúc phương Tây tại Sài Gòn.",
            "A prominent Catholic architectural symbol in the historic city center.",
            "Notre Dame Cathedral of Saigon is known for its red-brick facade, twin bell towers and central location near the Central Post Office. The surrounding area is a popular stop for architecture viewing, photos and experiencing the rhythm of Dong Khoi district."),
        new DemoPoi(
            "buu-dien-trung-tam",
            "Bưu điện Trung tâm Sài Gòn",
            "Saigon Central Post Office",
            10.779900m,
            106.699800m,
            50,
            ["architecture", "culture"],
            "Công trình bưu điện cổ nổi bật với mái vòm lớn, bản đồ xưa và không khí hoài niệm.",
            "Bưu điện Trung tâm Sài Gòn là một trong những công trình được yêu thích nhất tại khu trung tâm. Bước vào bên trong, du khách sẽ thấy mái vòm cao, các ô cửa giao dịch, bản đồ cổ và cảm giác giao thoa giữa chức năng công cộng với giá trị di sản. Đây cũng là điểm nối rất thuận tiện khi kết hợp tham quan Nhà thờ Đức Bà và Đường sách Nguyễn Văn Bình.",
            "A heritage post office with a grand hall, old maps and nostalgic public-service atmosphere.",
            "Saigon Central Post Office is one of the most beloved buildings in the city center. Its vaulted hall, service counters and old maps connect public function with heritage value. It is easy to combine with Notre Dame Cathedral and Nguyen Van Binh Book Street."),
        new DemoPoi(
            "cho-ben-thanh",
            "Chợ Bến Thành",
            "Ben Thanh Market",
            10.772100m,
            106.698300m,
            80,
            ["market", "culture"],
            "Khu chợ nổi tiếng với hàng lưu niệm, ẩm thực địa phương và nhịp mua bán sôi động.",
            "Chợ Bến Thành là điểm đến quen thuộc với cả du khách trong nước và quốc tế. Bên trong chợ có nhiều gian hàng bán đặc sản, vải vóc, đồ lưu niệm và món ăn địa phương. Không khí mua bán náo nhiệt giúp du khách cảm nhận rõ nhịp sống thương mại của Sài Gòn. Khi tham quan, nên đi chậm, hỏi giá rõ ràng và thử một vài món ăn nhẹ trong khu chợ.",
            "A famous market for souvenirs, local food and the lively rhythm of Saigon commerce.",
            "Ben Thanh Market is a familiar stop for domestic and international visitors. Its stalls sell local specialties, fabrics, souvenirs and food. The market atmosphere gives visitors a vivid sense of Saigon commerce and everyday life."),
        new DemoPoi(
            "bao-tang-chung-tich-chien-tranh",
            "Bảo tàng Chứng tích Chiến tranh",
            "War Remnants Museum",
            10.779400m,
            106.692000m,
            65,
            ["history", "culture"],
            "Bảo tàng chuyên đề giúp du khách tiếp cận ký ức chiến tranh qua tư liệu và hiện vật.",
            "Bảo tàng Chứng tích Chiến tranh là nơi lưu giữ nhiều hình ảnh, tài liệu và hiện vật liên quan đến chiến tranh tại Việt Nam. Không gian trưng bày có thể tạo cảm xúc mạnh, vì vậy du khách nên dành thời gian đọc kỹ thông tin và đi với thái độ tôn trọng. Đây là điểm quan trọng trong hành trình tìm hiểu lịch sử cận hiện đại của thành phố.",
            "A museum that presents wartime memory through documents, photographs and artifacts.",
            "The War Remnants Museum preserves photographs, documents and artifacts related to war in Vietnam. The exhibitions can be emotionally intense, so visitors should take time to read carefully and approach the displays respectfully."),
        new DemoPoi(
            "nha-hat-thanh-pho",
            "Nhà hát Thành phố Hồ Chí Minh",
            "Saigon Opera House",
            10.776700m,
            106.703100m,
            45,
            ["architecture", "culture"],
            "Nhà hát cổ nằm trên trục Đồng Khởi, nổi bật với mặt tiền trang trí tinh tế.",
            "Nhà hát Thành phố là một điểm nhấn kiến trúc tại khu Đồng Khởi. Công trình có mặt tiền cân đối, nhiều chi tiết trang trí và thường xuất hiện trong các tuyến đi bộ trung tâm. Dù chỉ ngắm từ bên ngoài, du khách vẫn có thể cảm nhận rõ vẻ sang trọng của khu phố cũ và vai trò của nghệ thuật biểu diễn trong đời sống đô thị.",
            "A historic theater on Dong Khoi Street with an elegant decorated facade.",
            "Saigon Opera House is an architectural highlight of Dong Khoi Street. Its balanced facade and decorative details make it a favorite stop on central walking routes and a symbol of performing arts in the city."),
        new DemoPoi(
            "pho-di-bo-nguyen-hue",
            "Phố đi bộ Nguyễn Huệ",
            "Nguyen Hue Walking Street",
            10.774300m,
            106.703900m,
            90,
            ["culture", "modern"],
            "Không gian đi bộ trung tâm, nơi diễn ra nhiều hoạt động cộng đồng và sự kiện đô thị.",
            "Phố đi bộ Nguyễn Huệ là trục không gian mở nối khu trung tâm với hướng sông Sài Gòn. Buổi tối, nơi đây thường đông người đi dạo, chụp ảnh và thưởng thức không khí thành phố. Hai bên tuyến phố có nhiều quán cà phê, trung tâm thương mại và công trình hiện đại. Đây là điểm rất phù hợp để kết thúc một ngày tham quan nhẹ nhàng.",
            "A central pedestrian boulevard for public activities, events and evening walks.",
            "Nguyen Hue Walking Street is an open urban axis leading toward the Saigon River. In the evening, it becomes a lively place for strolling, photos and city atmosphere, with cafes and modern buildings along both sides."),
        new DemoPoi(
            "bitexco-financial-tower",
            "Bitexco Financial Tower",
            "Bitexco Financial Tower",
            10.771600m,
            106.704400m,
            55,
            ["modern", "architecture"],
            "Tòa tháp biểu tượng của giai đoạn Sài Gòn hiện đại, nổi bật trong đường chân trời trung tâm.",
            "Bitexco Financial Tower từng là một trong những biểu tượng rõ nhất của Sài Gòn hiện đại. Hình dáng tòa tháp gợi liên tưởng đến búp sen và nổi bật khi nhìn từ khu Nguyễn Huệ hoặc ven sông. Đây là điểm phù hợp để nói về quá trình phát triển đô thị, tài chính và thương mại của TP.HCM trong những thập niên gần đây.",
            "A landmark tower representing modern Saigon and the city-center skyline.",
            "Bitexco Financial Tower is a strong symbol of modern Saigon. Its lotus-inspired form stands out from Nguyen Hue and the riverside, making it a good point to discuss urban, financial and commercial growth."),
        new DemoPoi(
            "bao-tang-my-thuat",
            "Bảo tàng Mỹ thuật TP.HCM",
            "Ho Chi Minh City Fine Arts Museum",
            10.769900m,
            106.699100m,
            55,
            ["culture", "architecture"],
            "Bảo tàng nghệ thuật trong công trình cổ, phù hợp cho người thích tranh, kiến trúc và nhiếp ảnh.",
            "Bảo tàng Mỹ thuật TP.HCM nằm trong một công trình cổ có màu sắc và chi tiết kiến trúc rất đặc trưng. Không gian trưng bày giới thiệu nhiều tác phẩm hội họa, điêu khắc và mỹ thuật ứng dụng. Với du khách yêu nghệ thuật, đây là nơi giúp nhìn Sài Gòn qua góc nhìn mềm mại hơn: không chỉ là lịch sử và thương mại mà còn là đời sống sáng tạo.",
            "An art museum in a heritage building, ideal for painting, architecture and photography lovers.",
            "Ho Chi Minh City Fine Arts Museum is housed in a distinctive heritage building. Its galleries introduce paintings, sculpture and applied arts, offering a softer creative perspective on Saigon beyond history and commerce."),
        new DemoPoi(
            "chua-ngoc-hoang",
            "Chùa Ngọc Hoàng",
            "Jade Emperor Pagoda",
            10.791700m,
            106.698000m,
            55,
            ["spiritual", "culture"],
            "Ngôi chùa nổi tiếng với không gian hương khói, tượng thờ và nét văn hóa tín ngưỡng đô thị.",
            "Chùa Ngọc Hoàng là điểm đến tâm linh có không khí rất riêng giữa thành phố đông đúc. Bên trong chùa có nhiều tượng thờ, chi tiết trang trí và không gian hương khói tạo cảm giác cổ kính. Khi tham quan, du khách nên ăn mặc lịch sự, nói chuyện nhỏ nhẹ và tôn trọng người đang hành lễ. Đây là điểm giúp hiểu thêm đời sống tín ngưỡng của cư dân Sài Gòn.",
            "A well-known spiritual site with incense, statues and urban religious culture.",
            "Jade Emperor Pagoda offers a distinctive spiritual atmosphere within the busy city. Its statues, decorative details and incense-filled rooms help visitors understand local religious life. Respectful behavior is recommended."),
        new DemoPoi(
            "thao-cam-vien-sai-gon",
            "Thảo Cầm Viên Sài Gòn",
            "Saigon Zoo and Botanical Gardens",
            10.787500m,
            106.705300m,
            100,
            ["park", "culture"],
            "Không gian xanh lâu đời, phù hợp cho gia đình, trẻ em và tuyến tham quan nhẹ trong ngày.",
            "Thảo Cầm Viên Sài Gòn là một trong những không gian xanh quen thuộc của thành phố. Khuôn viên có cây lớn, lối đi rộng, khu động vật và nhiều điểm nghỉ chân. Với gia đình có trẻ em, đây là điểm dừng dễ tiếp cận để kết hợp giữa vui chơi, học hỏi và thư giãn. Nên đi vào buổi sáng để thời tiết dễ chịu hơn.",
            "A long-standing green space for families, children and relaxed daytime visits.",
            "Saigon Zoo and Botanical Gardens is a familiar green space with large trees, walking paths, animal areas and rest stops. It is especially suitable for families and children, ideally in the morning."),
        new DemoPoi(
            "ben-nha-rong",
            "Bến Nhà Rồng",
            "Nha Rong Wharf",
            10.768100m,
            106.706900m,
            65,
            ["history", "river"],
            "Di tích ven sông gắn với lịch sử ra đi tìm đường cứu nước của Chủ tịch Hồ Chí Minh.",
            "Bến Nhà Rồng nằm bên sông Sài Gòn, là điểm đến gắn với ký ức lịch sử quan trọng. Công trình có vị trí nhìn ra khu cảng và đô thị ven sông, giúp du khách cảm nhận mối liên hệ giữa giao thương, hành trình và lịch sử. Đây là điểm phù hợp để kết nối tuyến tham quan trung tâm với câu chuyện về sông Sài Gòn.",
            "A riverside historical site connected with an important chapter of Vietnamese history.",
            "Nha Rong Wharf sits by the Saigon River and connects historical memory with the city’s trading and riverside identity. It links central sightseeing routes with the story of the Saigon River."),
        new DemoPoi(
            "chua-giac-lam",
            "Chùa Giác Lâm",
            "Giac Lam Pagoda",
            10.771500m,
            106.644600m,
            70,
            ["spiritual", "history"],
            "Ngôi chùa cổ nổi tiếng, thể hiện lớp di sản Phật giáo lâu đời của Sài Gòn.",
            "Chùa Giác Lâm là một điểm tham quan tâm linh quan trọng ở khu vực phía tây thành phố. Không gian chùa mang vẻ cổ kính với sân, tháp, tượng và các nếp sinh hoạt tôn giáo. Địa điểm này giúp du khách thấy rằng Sài Gòn không chỉ có khu trung tâm hiện đại mà còn có nhiều lớp văn hóa lâu đời trong các khu dân cư truyền thống.",
            "An old pagoda showing the long-standing Buddhist heritage of Saigon.",
            "Giac Lam Pagoda is an important spiritual site in the western part of the city. Its courtyards, towers, statues and religious routines reveal older cultural layers beyond the modern city center."),
        new DemoPoi(
            "cho-binh-tay",
            "Chợ Bình Tây",
            "Binh Tay Market",
            10.749900m,
            106.651100m,
            95,
            ["market", "culture"],
            "Khu chợ lớn của Chợ Lớn, nổi bật với hoạt động buôn bán sỉ và kiến trúc đặc trưng.",
            "Chợ Bình Tây là điểm nhấn của khu Chợ Lớn với không khí thương mại sôi động. Nơi đây phù hợp để quan sát nhịp buôn bán, cách sắp xếp hàng hóa và nét văn hóa cộng đồng người Hoa tại Sài Gòn. Khi đi chợ, du khách nên giữ đồ cá nhân cẩn thận, đi theo nhóm nhỏ và dành thời gian khám phá các tuyến phố lân cận.",
            "A major Chinatown market known for wholesale trade and distinctive architecture.",
            "Binh Tay Market is a key landmark in Cho Lon with lively trade, dense merchandise and Chinese-Vietnamese community culture. It is best explored slowly with attention to personal belongings."),
        new DemoPoi(
            "pho-di-bo-bui-vien",
            "Phố đi bộ Bùi Viện",
            "Bui Vien Walking Street",
            10.767700m,
            106.693300m,
            85,
            ["culture", "modern"],
            "Khu phố giải trí sôi động, nổi bật về đêm với âm nhạc, ẩm thực và du khách quốc tế.",
            "Phố đi bộ Bùi Viện là khu vực giải trí nổi tiếng về đêm tại trung tâm thành phố. Không khí nơi đây sôi động, nhiều âm thanh, ánh sáng, quán ăn và quán nước. Địa điểm phù hợp với du khách muốn cảm nhận nhịp sống trẻ, nhưng không nên xem đây là không gian yên tĩnh hay truyền thống. Hãy đi cùng bạn bè và chú ý an toàn cá nhân vào giờ khuya.",
            "A lively nightlife street with music, food and international visitors.",
            "Bui Vien Walking Street is a famous nightlife area with music, lights, food and drinks. It suits visitors seeking a youthful atmosphere, but it is not a quiet or traditional space. Personal safety is important late at night."),
        new DemoPoi(
            "landmark-81",
            "Landmark 81",
            "Landmark 81",
            10.794800m,
            106.721900m,
            90,
            ["modern", "architecture"],
            "Tòa nhà cao tầng biểu tượng của Sài Gòn mới, nằm trong khu đô thị ven sông hiện đại.",
            "Landmark 81 đại diện cho hình ảnh TP.HCM hiện đại với skyline cao tầng và không gian thương mại, giải trí ven sông. Từ khu vực xung quanh, du khách có thể cảm nhận sự thay đổi nhanh của đô thị về phía đông. Đây là điểm phù hợp cho tuyến tham quan buổi tối, đặc biệt khi kết hợp với cầu Ba Son và các góc nhìn sông Sài Gòn.",
            "A skyscraper symbol of new Saigon within a modern riverside urban area.",
            "Landmark 81 represents modern Ho Chi Minh City with a tall skyline and riverside commercial spaces. The area shows the city’s rapid eastward urban transformation and works well for evening routes."),
        new DemoPoi(
            "cau-ba-son",
            "Cầu Ba Son",
            "Ba Son Bridge",
            10.785300m,
            106.713500m,
            80,
            ["modern", "river"],
            "Cây cầu kết nối trung tâm với khu đô thị mới, có góc nhìn đẹp về sông và skyline.",
            "Cầu Ba Son là điểm quan sát thú vị để nhìn về sông Sài Gòn và các lớp phát triển đô thị hai bên bờ. Cây cầu kết nối khu trung tâm cũ với khu đô thị mới, vì vậy rất phù hợp để kể câu chuyện về sự mở rộng của TP.HCM. Buổi chiều và tối là thời điểm đẹp để ngắm ánh sáng thành phố từ khu vực này.",
            "A bridge linking the old center with new urban areas and offering skyline views.",
            "Ba Son Bridge is a good viewpoint over the Saigon River and the city skyline. It connects the old center with newer urban districts, making it useful for explaining the city’s expansion."),
        new DemoPoi(
            "cong-vien-tao-dan",
            "Công viên Tao Đàn",
            "Tao Dan Park",
            10.774500m,
            106.692200m,
            95,
            ["park", "culture"],
            "Mảng xanh trung tâm, phù hợp để nghỉ chân giữa các điểm tham quan lịch sử và bảo tàng.",
            "Công viên Tao Đàn là không gian xanh dễ tiếp cận ngay khu trung tâm. Với hàng cây lớn, lối đi rộng và nhiều khu nghỉ chân, công viên phù hợp để tạm dừng giữa hành trình tham quan Dinh Độc Lập, bảo tàng hoặc các tuyến phố lân cận. Buổi sáng, nơi đây thường có không khí sinh hoạt địa phương rất rõ nét.",
            "A central green space ideal for resting between museums and historic landmarks.",
            "Tao Dan Park is an accessible green space in the city center. Its large trees, paths and resting areas make it suitable for a pause between Independence Palace, museums and nearby streets."),
        new DemoPoi(
            "duong-sach-nguyen-van-binh",
            "Đường sách Nguyễn Văn Bình",
            "Nguyen Van Binh Book Street",
            10.779600m,
            106.699500m,
            45,
            ["culture", "architecture"],
            "Con đường sách nhỏ cạnh Nhà thờ Đức Bà và Bưu điện, phù hợp để nghỉ chân và mua sách.",
            "Đường sách Nguyễn Văn Bình là không gian văn hóa nhỏ nhưng rất dễ thương trong khu trung tâm. Hai bên đường có nhà sách, quầy cà phê, khu đọc sách và các hoạt động giao lưu. Vì nằm gần Nhà thờ Đức Bà và Bưu điện Trung tâm, đây là điểm dừng lý tưởng để kết thúc cụm tham quan kiến trúc bằng một khoảng nghỉ nhẹ nhàng.",
            "A charming book street beside Notre Dame Cathedral and the Central Post Office.",
            "Nguyen Van Binh Book Street is a small cultural space with bookstores, cafes, reading corners and events. Its location near Notre Dame and the Central Post Office makes it an ideal relaxed stop."),
        new DemoPoi(
            "bao-tang-lich-su-tphcm",
            "Bảo tàng Lịch sử TP.HCM",
            "Ho Chi Minh City Museum of History",
            10.788000m,
            106.705300m,
            65,
            ["history", "culture"],
            "Bảo tàng giới thiệu nhiều lớp lịch sử, văn hóa và hiện vật của vùng đất phương Nam.",
            "Bảo tàng Lịch sử TP.HCM nằm gần Thảo Cầm Viên, thuận tiện cho tuyến tham quan gia đình và giáo dục. Không gian trưng bày giúp du khách tiếp cận các giai đoạn lịch sử, văn hóa và hiện vật khảo cổ của vùng đất phương Nam. Đây là điểm phù hợp để bổ sung chiều sâu kiến thức sau khi đã tham quan các công trình ngoài trời.",
            "A history museum presenting cultural layers and artifacts of southern Vietnam.",
            "Ho Chi Minh City Museum of History is located near the zoo and works well for family and educational routes. Its exhibits introduce historical periods, cultural layers and artifacts from southern Vietnam.")
    ];

    private sealed record DemoLanguage(string Code, string Name, string EdgeTtsVoice);
    private sealed record DemoCategory(string Key, string ViName, string EnName, string IconUrl);
    private sealed record DemoTourist(string Email, string FullName, string Password);
    private sealed record DemoTour(
        string Slug,
        string Title,
        string Description,
        string EnTitle,
        string EnDescription,
        int DurationMinutes,
        IReadOnlyList<string> PoiSlugs);

    private sealed record DemoPoi(
        string Slug,
        string Name,
        string EnName,
        decimal Latitude,
        decimal Longitude,
        int Radius,
        IReadOnlyList<string> CategoryKeys,
        string ShortDescription,
        string FullDescription,
        string EnShortDescription,
        string EnFullDescription);
}
