using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Text;

namespace AdminWeb.Services;

public sealed class DemoDataSeeder
{
    private const string SeedKey = "DemoData:SaigonFullV2";
    private const string OwnerMenuSeedKey = "DemoData:SaigonOwnerPoiMenuV1";
    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly ILogger<DemoDataSeeder> _logger;
    private readonly IWebHostEnvironment _environment;

    public DemoDataSeeder(
        AppDbContext context,
        PasswordService passwordService,
        ILogger<DemoDataSeeder> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _passwordService = passwordService;
        _logger = logger;
        _environment = environment;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await EnsureFiveLanguagePoiTranslationsAsync(now, cancellationToken);
        await SeedRealOwnerPoiMenuAsync(now, cancellationToken);
        await EnsureOwnerMenuItemImagesAsync(now, cancellationToken);
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
            new DemoCategory("river", "Sông nước", "Riverside", "/uploads/demo/icons/river.svg"),
            new DemoCategory("museum", "Bảo tàng", "Museums", "/uploads/demo/icons/museum.svg"),
            new DemoCategory("nature", "Thiên nhiên", "Nature", "/uploads/demo/icons/nature.svg"),
            new DemoCategory("shopping", "Mua sắm", "Shopping", "/uploads/demo/icons/shopping.svg"),
            new DemoCategory("entertainment", "Giải trí", "Entertainment", "/uploads/demo/icons/entertainment.svg"),
            new DemoCategory("food", "Ẩm thực", "Food & Drink", "/uploads/demo/icons/food.svg"),
            new DemoCategory("family", "Dành cho gia đình", "Family Friendly", "/uploads/demo/icons/family.svg")
        };

        foreach (var item in categories)
        {
            await EnsureDemoCategoryIconSvgAsync(item, cancellationToken);

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

    private async Task EnsureDemoCategoryIconSvgAsync(DemoCategory category, CancellationToken cancellationToken)
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var directory = Path.Combine(webRoot, "uploads", "demo", "icons");
        var physicalPath = Path.Combine(directory, $"{category.Key}.svg");
        if (File.Exists(physicalPath))
            return;

        Directory.CreateDirectory(directory);
        var label = WebUtility.HtmlEncode(category.ViName);
        var initial = WebUtility.HtmlEncode(category.EnName[..1].ToUpperInvariant());
        var svg = $"""
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256" role="img" aria-label="{label}">
  <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#0f766e"/><stop offset="1" stop-color="#22c55e"/></linearGradient></defs>
  <rect width="256" height="256" rx="64" fill="url(#g)"/>
  <circle cx="128" cy="112" r="68" fill="#ffffff" opacity=".17"/>
  <text x="128" y="145" text-anchor="middle" font-family="Arial, sans-serif" font-size="92" font-weight="800" fill="#ffffff">{initial}</text>
  <rect x="40" y="202" width="176" height="8" rx="4" fill="#ffffff" opacity=".65"/>
</svg>
""";

        await File.WriteAllTextAsync(physicalPath, svg, Encoding.UTF8, cancellationToken);
    }

    private async Task<List<int>> SeedTouristsAsync(CancellationToken cancellationToken)
    {
        var tourists = new[]
        {
            new DemoTourist("tourist@local", "Du khách Demo", "tourist123"),
            new DemoTourist("ngocanh.demo@versa.local", "Ngọc Anh", "123456"),
            new DemoTourist("minhkhoi.demo@versa.local", "Minh Khôi", "123456"),
            new DemoTourist("lanphuong.demo@versa.local", "Lan Phương", "123456"),
            new DemoTourist("jaeho.demo@versa.local", "Jae Ho", "123456"),
            new DemoTourist("thanhha.demo@versa.local", "Thanh Hà", "123456"),
            new DemoTourist("quanghuy.demo@versa.local", "Quang Huy", "123456"),
            new DemoTourist("maianh.demo@versa.local", "Mai Anh", "123456"),
            new DemoTourist("baotran.demo@versa.local", "Bảo Trân", "123456"),
            new DemoTourist("hoangnam.demo@versa.local", "Hoàng Nam", "123456"),
            new DemoTourist("yuki.demo@versa.local", "Yuki Tanaka", "123456"),
            new DemoTourist("sora.demo@versa.local", "Sora Kim", "123456"),
            new DemoTourist("xiaoyu.demo@versa.local", "Xiao Yu", "123456"),
            new DemoTourist("olivia.demo@versa.local", "Olivia Brown", "123456"),
            new DemoTourist("lucas.demo@versa.local", "Lucas Martin", "123456"),
            new DemoTourist("thuylinh.demo@versa.local", "Thùy Linh", "123456"),
            new DemoTourist("ducminh.demo@versa.local", "Đức Minh", "123456"),
            new DemoTourist("phuongvy.demo@versa.local", "Phương Vy", "123456"),
            new DemoTourist("giahuy.demo@versa.local", "Gia Huy", "123456"),
            new DemoTourist("khanhngan.demo@versa.local", "Khánh Ngân", "123456")
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
            await EnsureDemoPoiCoverSvgAsync(item, cancellationToken);

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
                ["cong-vien-tao-dan", "thao-cam-vien-sai-gon", "bao-tang-lich-su-tphcm", "duong-sach-nguyen-van-binh"]),
            new DemoTour(
                "tour-sacred-saigon",
                "Sắc màu tôn giáo Sài Gòn",
                "Một vòng qua nhà thờ, chùa, hội quán và đền Hindu để khám phá đời sống tín ngưỡng đa dạng của thành phố.",
                "Sacred Saigon",
                "A route through churches, pagodas, assembly halls and a Hindu temple reflecting the city's diverse faiths.",
                240,
                ["nha-tho-tan-dinh", "chua-vinh-nghiem", "viet-nam-quoc-tu", "chua-xa-loi", "den-mariamman"]),
            new DemoTour(
                "tour-cholon-deep-dive",
                "Một ngày khám phá Chợ Lớn",
                "Hành trình trọn vẹn qua chợ, hội quán, chùa Bà và nhà thờ cổ trong khu phố Hoa lâu đời.",
                "Chinatown Deep Dive",
                "A full heritage day through markets, assembly halls, temples and an old church in Cho Lon.",
                270,
                ["cho-binh-tay", "chua-ba-thien-hau", "chua-quan-am", "hoi-quan-ha-chuong", "nha-tho-cha-tam", "cho-an-dong"]),
            new DemoTour(
                "tour-museum-marathon",
                "Ngày hội bảo tàng Sài Gòn",
                "Tuyến chuyên sâu dành cho người yêu lịch sử, mỹ thuật, y học và những câu chuyện về con người phương Nam.",
                "Saigon Museum Marathon",
                "An in-depth route for visitors interested in history, fine arts, medicine and southern Vietnamese stories.",
                300,
                ["bao-tang-tphcm", "bao-tang-my-thuat", "bao-tang-phu-nu-nam-bo", "bao-tang-chien-dich-ho-chi-minh", "bao-tang-y-hoc-co-truyen"]),
            new DemoTour(
                "tour-riverside-sunset",
                "Hoàng hôn bên sông Sài Gòn",
                "Tuyến chiều tối kết nối công viên ven sông, bến tàu, cầu hiện đại và các góc ngắm đường chân trời.",
                "Saigon Riverside Sunset",
                "An afternoon-to-evening route linking riverside parks, water transport and skyline viewpoints.",
                180,
                ["cong-vien-ben-bach-dang", "ga-tau-thuy-bach-dang", "cau-ba-son", "cong-vien-bo-song-sai-gon", "cong-vien-sala"]),
            new DemoTour(
                "tour-local-markets",
                "Nhịp sống những khu chợ",
                "Khám phá hoa, vải vóc, thực phẩm và nhịp buôn bán địa phương ở nhiều khu vực của thành phố.",
                "Local Market Rhythms",
                "Explore flowers, textiles, food and everyday commerce across several lively city markets.",
                240,
                ["cho-ho-thi-ky", "cho-tan-dinh", "cho-an-dong", "cho-binh-tay", "cho-ben-thanh"]),
            new DemoTour(
                "tour-parks-and-lakes",
                "Công viên, hồ nước & cầu ánh sao",
                "Một ngày thư giãn qua các mảng xanh lớn, hồ Bán Nguyệt và không gian đi bộ phía nam thành phố.",
                "Parks, Lakes & Starlight Bridge",
                "A relaxed day through large green spaces, Crescent Lake and pedestrian areas in southern Saigon.",
                220,
                ["cong-vien-gia-dinh", "cong-vien-hoang-van-thu", "ho-ban-nguyet", "cau-anh-sao"]),
            new DemoTour(
                "tour-family-fun-day",
                "Một ngày vui chơi gia đình",
                "Gợi ý vui chơi cả ngày với công viên văn hóa, khu giải trí và không gian nghệ thuật tương tác.",
                "Family Fun Day",
                "A full family day featuring cultural parks, theme attractions and interactive art.",
                360,
                ["cong-vien-van-hoa-dam-sen", "khu-du-lich-suoi-tien", "artinus-3d-art-museum"]),
            new DemoTour(
                "tour-ao-dai-and-culture",
                "Áo dài & câu chuyện phụ nữ Nam Bộ",
                "Tuyến văn hóa kết nối áo dài Việt Nam, ký ức phụ nữ phương Nam và không gian nghệ thuật thành phố.",
                "Ao Dai & Southern Women's Stories",
                "A cultural route connecting Vietnamese ao dai, southern women's history and city art spaces.",
                210,
                ["bao-tang-ao-dai", "bao-tang-phu-nu-nam-bo", "bao-tang-my-thuat", "nha-hat-thanh-pho"]),
            new DemoTour(
                "tour-can-gio-eco",
                "Sinh thái Cần Giờ",
                "Chuyến đi ngoại thành khám phá rừng ngập mặn, đảo khỉ, căn cứ lịch sử và không khí biển Cần Giờ.",
                "Can Gio Eco Adventure",
                "An out-of-town journey through mangroves, Monkey Island, a historic base and the Can Gio coast.",
                480,
                ["khu-du-tru-sinh-quyen-can-gio", "dao-khi-can-gio", "chien-khu-rung-sac", "bien-30-thang-4-can-gio"]),
            new DemoTour(
                "tour-hidden-neighborhoods",
                "Góc phố Sài Gòn ít người biết",
                "Tuyến đi chậm qua hẻm chung cư, phố đồ cổ, hồ Con Rùa và khu phố Nhật giữa lòng đô thị.",
                "Hidden Saigon Neighborhoods",
                "A slow route through apartment alleys, antique street, Turtle Lake and the city's Japan Town.",
                210,
                ["chung-cu-nguyen-thien-thuat", "pho-do-co-le-cong-kieu", "ho-con-rua", "khu-pho-nhat-le-thanh-ton"]),
            new DemoTour(
                "tour-thanh-da-retreat",
                "Trốn phố ở Thanh Đa - Bình Quới",
                "Nửa ngày thư giãn bên sông với bán đảo Thanh Đa, làng du lịch và những khoảng xanh gần trung tâm.",
                "Thanh Da Riverside Retreat",
                "A half-day riverside escape through Thanh Da peninsula and the green spaces of Binh Quoi.",
                240,
                ["ban-dao-thanh-da", "khu-du-lich-binh-quoi", "khu-du-lich-van-thanh"]),
            new DemoTour(
                "tour-city-icons-photo",
                "Săn ảnh biểu tượng thành phố",
                "Lộ trình dành cho người thích nhiếp ảnh với kiến trúc hành chính, nhà thờ màu hồng, skyline và cầu ven sông.",
                "City Icons Photo Walk",
                "A photography route featuring civic architecture, the pink church, skyline towers and riverside bridges.",
                200,
                ["tru-so-ubnd-tphcm", "nha-tho-tan-dinh", "bitexco-financial-tower", "landmark-81", "cau-anh-sao"])
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
            for (var i = 0; i < Math.Min(8, touristIds.Count); i++)
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

        foreach (var touristId in touristIds.Take(16))
        {
            foreach (var poiId in orderedPois.Where((_, index) => (index + touristId) % 4 == 0).Take(12))
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

        var poisWithDemoLogs = await _context.VisitorPlaybackLogs
            .Where(item => item.DeviceId.StartsWith("demo-saigon-"))
            .Select(item => item.PoiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var loggedPoiIds = poisWithDemoLogs.ToHashSet();
        var languages = new[] { "vi", "en", "zh", "ja", "ko" };
        var triggers = new[] { "GPS", "QR", "WebMap", "MobileDetail" };
        var logIndex = await _context.VisitorPlaybackLogs
            .CountAsync(item => item.DeviceId.StartsWith("demo-saigon-"), cancellationToken);

        foreach (var poiId in orderedPois.Where(poiId => !loggedPoiIds.Contains(poiId)))
        {
            var logCount = random.Next(8, 19);
            for (var i = 0; i < logCount; i++)
            {
                var touristId = random.NextDouble() > 0.2
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
                    ListenDuration = random.Next(35, 360),
                    CreatedAt = now.AddDays(-random.Next(0, 30)).AddMinutes(-random.Next(0, 1440))
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDemoPoiCoverSvgAsync(DemoPoi poi, CancellationToken cancellationToken)
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var directory = Path.Combine(webRoot, "uploads", "demo", "poi-covers");
        var physicalPath = Path.Combine(directory, $"{poi.Slug}.svg");
        if (File.Exists(physicalPath))
            return;

        Directory.CreateDirectory(directory);
        var palettes = new[]
        {
            (Start: "#0f766e", End: "#14b8a6", Accent: "#99f6e4"),
            (Start: "#9a3412", End: "#f97316", Accent: "#fed7aa"),
            (Start: "#1d4ed8", End: "#38bdf8", Accent: "#bae6fd"),
            (Start: "#6b21a8", End: "#c084fc", Accent: "#e9d5ff"),
            (Start: "#166534", End: "#65a30d", Accent: "#d9f99d")
        };
        var paletteIndex = poi.Slug.Aggregate(0, (sum, ch) => (sum + ch) % palettes.Length);
        var palette = palettes[paletteIndex];
        var safeViName = WebUtility.HtmlEncode(poi.Name);
        var safeEnName = WebUtility.HtmlEncode(poi.EnName);
        var safeCategory = WebUtility.HtmlEncode(string.Join(" • ", poi.CategoryKeys.Select(key => key.ToUpperInvariant())));
        var initials = WebUtility.HtmlEncode(string.Concat(poi.EnName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(3)
            .Select(word => char.ToUpperInvariant(word[0]))));

        var svg = $"""
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="720" viewBox="0 0 1200 720" role="img" aria-label="{safeViName}">
  <defs>
    <linearGradient id="background" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="{palette.Start}"/>
      <stop offset="1" stop-color="{palette.End}"/>
    </linearGradient>
    <filter id="shadow"><feDropShadow dx="0" dy="14" stdDeviation="18" flood-opacity=".25"/></filter>
  </defs>
  <rect width="1200" height="720" fill="url(#background)"/>
  <circle cx="1060" cy="80" r="250" fill="{palette.Accent}" opacity=".15"/>
  <circle cx="90" cy="680" r="290" fill="#ffffff" opacity=".08"/>
  <path d="M0 590 C250 510 380 690 650 590 S990 500 1200 610 V720 H0Z" fill="#071a2b" opacity=".2"/>
  <g filter="url(#shadow)">
    <rect x="90" y="92" width="230" height="230" rx="54" fill="#ffffff" opacity=".94"/>
    <text x="205" y="235" text-anchor="middle" font-family="Arial, sans-serif" font-size="70" font-weight="800" fill="{palette.Start}">{initials}</text>
  </g>
  <text x="90" y="410" font-family="Arial, sans-serif" font-size="24" font-weight="700" fill="{palette.Accent}" letter-spacing="3">VERSA CITY GUIDE</text>
  <text x="90" y="482" font-family="Arial, sans-serif" font-size="46" font-weight="800" fill="#ffffff">{safeViName}</text>
  <text x="90" y="532" font-family="Arial, sans-serif" font-size="26" fill="#ffffff" opacity=".86">{safeEnName}</text>
  <rect x="90" y="576" width="650" height="54" rx="27" fill="#ffffff" opacity=".14"/>
  <text x="120" y="611" font-family="Arial, sans-serif" font-size="19" font-weight="700" fill="#ffffff">{safeCategory}</text>
</svg>
""";

        await File.WriteAllTextAsync(physicalPath, svg, Encoding.UTF8, cancellationToken);
    }


    private async Task SeedRealOwnerPoiMenuAsync(DateTime now, CancellationToken cancellationToken)
    {
        var alreadySeeded = await _context.SystemSettings
            .AnyAsync(item => item.SettingKey == OwnerMenuSeedKey, cancellationToken);

        if (alreadySeeded)
            return;

        _logger.LogInformation("Seeding real Ho Chi Minh City owner POI menu demo data...");

        var ownerRole = await _context.Roles
            .FirstOrDefaultAsync(role => role.RoleName == "Owner", cancellationToken);

        if (ownerRole == null)
        {
            ownerRole = new Role
            {
                RoleName = "Owner",
                Description = "Chủ gian hàng / chủ POI"
            };

            _context.Roles.Add(ownerRole);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var categoryIds = await SeedCategoriesAsync(cancellationToken);
        var tourist = await _context.Tourists.FirstOrDefaultAsync(item => item.Email == "tourist@local", cancellationToken);
        if (tourist == null)
        {
            tourist = new Tourist
            {
                Email = "tourist@local",
                FullName = "Du khách Demo",
                PasswordHash = _passwordService.Hash("tourist123"),
                AuthProvider = "local",
                CreatedAt = now.AddDays(-30)
            };
            _context.Tourists.Add(tourist);
            await _context.SaveChangesAsync(cancellationToken);
        }

        foreach (var seed in BuildRealOwnerPoiMenus())
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(item => item.Username == seed.Username || item.Email == seed.Email, cancellationToken);

            if (user == null)
            {
                user = new User
                {
                    RoleId = ownerRole.Id,
                    Username = seed.Username,
                    Email = seed.Email,
                    PasswordHash = _passwordService.Hash(seed.Password),
                    Status = "active",
                    CreatedAt = now.AddDays(-20)
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                user.RoleId = ownerRole.Id;
                user.Status = "active";
            }

            var owner = await _context.OwnerProfiles
                .Include(item => item.Pois)
                .FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);

            if (owner == null)
            {
                owner = new OwnerProfile
                {
                    UserId = user.Id,
                    BusinessName = seed.BusinessName,
                    RepresentativeName = seed.RepresentativeName,
                    Phone = seed.Phone,
                    Address = seed.Address,
                    Status = "Active",
                    CreatedAt = now.AddDays(-18)
                };

                _context.OwnerProfiles.Add(owner);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                owner.BusinessName = seed.BusinessName;
                owner.RepresentativeName = seed.RepresentativeName;
                owner.Phone = seed.Phone;
                owner.Address = seed.Address;
                owner.Status = "Active";
            }

            var poiId = await _context.PoiTranslations
                .Where(item => item.LanguageCode == "vi" && item.Name == seed.PoiName)
                .Select(item => (int?)item.PoiId)
                .FirstOrDefaultAsync(cancellationToken);

            Poi poi;
            if (poiId.HasValue)
            {
                poi = await _context.Pois
                    .Include(item => item.Translations)
                    .Include(item => item.PoiCategories)
                    .FirstAsync(item => item.Id == poiId.Value, cancellationToken);

                poi.OwnerProfileId = owner.Id;
                poi.Latitude = seed.Latitude;
                poi.Longitude = seed.Longitude;
                poi.Radius = seed.Radius;
                poi.Status = "Approved";
                poi.CoverImageUrl = $"/uploads/demo/owner-poi-covers/{seed.Slug}.svg";
            }
            else
            {
                poi = new Poi
                {
                    OwnerProfileId = owner.Id,
                    CreatedBy = user.Id,
                    Latitude = seed.Latitude,
                    Longitude = seed.Longitude,
                    Radius = seed.Radius,
                    QrCodeToken = Guid.NewGuid().ToString("N"),
                    Status = "Approved",
                    AdminNote = "Dữ liệu chủ POI thật tại TP.HCM, thêm sẵn để demo Owner/Menu.",
                    CoverImageUrl = $"/uploads/demo/owner-poi-covers/{seed.Slug}.svg",
                    CreatedAt = now.AddDays(-15),
                    Translations =
                    [
                        new PoiTranslation
                        {
                            LanguageCode = "vi",
                            Name = seed.PoiName,
                            ShortDescription = seed.ShortDescription,
                            FullDescription = seed.FullDescription,
                            TtsScript = seed.FullDescription,
                            UpdatedAt = now
                        },
                        new PoiTranslation
                        {
                            LanguageCode = "en",
                            Name = seed.PoiNameEn,
                            ShortDescription = seed.ShortDescriptionEn,
                            FullDescription = seed.FullDescriptionEn,
                            TtsScript = seed.FullDescriptionEn,
                            UpdatedAt = now
                        }
                    ],
                    MediaAssets =
                    [
                        new MediaAsset
                        {
                            MediaType = "image",
                            MediaUrl = $"/uploads/demo/owner-poi-covers/{seed.Slug}.svg",
                            SortOrder = 1
                        }
                    ],
                    Beacons =
                    [
                        new Beacon
                        {
                            Uuid = "fda50693-a4e2-4fb1-afcf-c6eb07647825",
                            Major = 200,
                            Minor = 2000 + seed.Order,
                            PlacementNote = $"Demo beacon khu vực quầy của {seed.PoiName}."
                        }
                    ]
                };

                foreach (var categoryKey in seed.CategoryKeys)
                {
                    if (categoryIds.TryGetValue(categoryKey, out var categoryId))
                    {
                        poi.PoiCategories.Add(new PoiCategory { CategoryId = categoryId });
                    }
                }

                _context.Pois.Add(poi);
                await _context.SaveChangesAsync(cancellationToken);
            }

            foreach (var menu in seed.MenuItems)
            {
                var exists = await _context.OwnerMenuItems.AnyAsync(item =>
                    item.OwnerProfileId == owner.Id &&
                    item.PoiId == poi.Id &&
                    item.Name == menu.Name,
                    cancellationToken);

                if (exists)
                    continue;

                _context.OwnerMenuItems.Add(new OwnerMenuItem
                {
                    OwnerProfileId = owner.Id,
                    PoiId = poi.Id,
                    Name = menu.Name,
                    Description = menu.Description,
                    Price = menu.Price,
                    Currency = "VND",
                    ImageUrl = GetDemoMenuItemImageUrl(menu.Name),
                    Status = "Active",
                    CreatedAt = now.AddDays(-10),
                    UpdatedAt = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            var orderExists = await _context.MenuOrders.AnyAsync(item =>
                item.OrderCode == $"REAL-{seed.Order:00}-DEMO", cancellationToken);

            if (!orderExists)
            {
                var selectedItems = await _context.OwnerMenuItems
                    .Where(item => item.OwnerProfileId == owner.Id && item.PoiId == poi.Id && item.Status == "Active")
                    .OrderBy(item => item.Id)
                    .Take(2)
                    .ToListAsync(cancellationToken);

                if (selectedItems.Count > 0)
                {
                    var subtotal = selectedItems.Sum(item => item.Price);
                    var order = new MenuOrder
                    {
                        OrderCode = $"REAL-{seed.Order:00}-DEMO",
                        TouristId = tourist.Id,
                        OwnerProfileId = owner.Id,
                        PoiId = poi.Id,
                        CustomerName = "Du khách Demo",
                        CustomerPhone = "0909000000",
                        Note = "Đơn demo tạo sẵn để chủ POI kiểm tra màn hình đơn hàng.",
                        Status = seed.Order % 2 == 0 ? "Confirmed" : "Pending",
                        PaymentMethod = "PayAtCounter",
                        PaymentStatus = "Unpaid",
                        Subtotal = subtotal,
                        TotalAmount = subtotal,
                        Currency = "VND",
                        CreatedAt = now.AddDays(-seed.Order)
                    };

                    foreach (var item in selectedItems)
                    {
                        order.Items.Add(new MenuOrderItem
                        {
                            OwnerMenuItemId = item.Id,
                            ItemName = item.Name,
                            UnitPrice = item.Price,
                            Quantity = 1,
                            LineTotal = item.Price,
                            Currency = "VND"
                        });
                    }

                    _context.MenuOrders.Add(order);
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
        }

        _context.SystemSettings.Add(new SystemSetting
        {
            SettingKey = OwnerMenuSeedKey,
            SettingValue = DateTime.UtcNow.ToString("O"),
            Description = "Đánh dấu đã thêm dữ liệu chủ POI, quán ăn/cafe thật ở TP.HCM, menu và đơn demo. Giá menu dùng cho demo và có thể thay đổi ngoài thực tế."
        });

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Real Ho Chi Minh City owner POI menu demo data seeded successfully.");
    }


    private async Task EnsureOwnerMenuItemImagesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var allItems = await _context.OwnerMenuItems
            .ToListAsync(cancellationToken);

        var items = allItems
            .Where(item => string.IsNullOrWhiteSpace(item.ImageUrl) ||
                           item.ImageUrl.StartsWith("/uploads/demo/menu-items/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (items.Count == 0)
            return;

        foreach (var item in items)
        {
            var imageUrl = GetDemoMenuItemImageUrl(item.Name);

            if (string.Equals(item.ImageUrl, imageUrl, StringComparison.OrdinalIgnoreCase))
                continue;

            item.ImageUrl = imageUrl;
            item.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string GetDemoMenuItemImageUrl(string name)
    {
        var slug = ToSafeSlug(name);
        var fileName = slug switch
        {
            var item when item.Contains("banh-bao") => "photo-banh-bao.png",
            var item when item.Contains("com-chay") => "photo-com-chay.png",
            var item when item.Contains("banh-mi") => "photo-banh-mi.png",
            var item when item.Contains("bo-tuoi") || item.Contains("pate") => "photo-pate-bo.png",

            var item when item.Contains("pho-tho-da") => "photo-pho-tho-da.png",
            var item when item.Contains("pho") || item.Contains("quay") || item.Contains("chen-thit-them") => "photo-pho-bo.png",

            var item when item.Contains("nuoc-sam") => "photo-nuoc-sam.png",
            var item when item.Contains("ca-phe") || item.Contains("dua-tuoi") || item.Contains("tra-dao") => "photo-drinks.png",

            var item when item.Contains("cha-trung") ||
                          item.Contains("bi-heo") ||
                          item.Contains("trung-op-la") ||
                          item.Contains("canh") ||
                          item.Contains("do-chua") => "photo-com-tam-side.png",
            var item when item.Contains("suon-nuong") => "photo-suon-nuong.png",
            var item when item.Contains("com-tam") => "photo-com-tam.png",

            var item when item.Contains("spicy-garlic-shrimp") => "photo-pizza-shrimp.png",
            var item when item.Contains("burrata") || item.Contains("parma") => "photo-pizza-burrata-parma.png",
            var item when item.Contains("salami") || item.Contains("chorizo") => "photo-pizza-salami.png",
            var item when item.Contains("margherita") || item.Contains("pizza") => "photo-pizza-margherita.png",
            var item when item.Contains("truffle") || item.Contains("fries") => "photo-truffle-fries.png",
            var item when item.Contains("green-salad") || item.Contains("salad") => "photo-green-salad.png",

            var item when item.Contains("uu-dai") => "photo-drinks.png",
            var item when item.Contains("combo") => "photo-com-tam.png",
            _ => "photo-banh-mi.png"
        };

        return $"/uploads/demo/menu-items/{fileName}";
    }

    private static string ToSafeSlug(string value)
    {
        value = (value ?? string.Empty).Replace('đ', 'd').Replace('Đ', 'd');
        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var lastWasDash = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "menu-item" : result;
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
                var localizedTranslation = CreateLocalizedPoiTranslation(language, source, now);
                var existing = poi.Translations.FirstOrDefault(item =>
                    string.Equals(item.LanguageCode, language.Code, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    poi.Translations.Add(localizedTranslation);
                    continue;
                }

                if (ShouldRefreshLocalizedPoiName(existing, localizedTranslation, source))
                {
                    existing.Name = localizedTranslation.Name;
                    existing.UpdatedAt = now;
                }
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
        var sourceName = string.IsNullOrWhiteSpace(source.Name) ? "POI" : source.Name.Trim();
        var name = GetLocalizedPoiName(language.Code, sourceName);

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

    private static bool ShouldRefreshLocalizedPoiName(
        PoiTranslation existing,
        PoiTranslation localizedTranslation,
        PoiTranslation source)
    {
        if (string.IsNullOrWhiteSpace(localizedTranslation.Name))
            return false;

        var existingName = existing.Name?.Trim() ?? string.Empty;
        var localizedName = localizedTranslation.Name.Trim();
        var sourceName = source.Name?.Trim() ?? string.Empty;

        if (string.Equals(existingName, localizedName, StringComparison.OrdinalIgnoreCase))
            return false;

        // Không tự ghi đè tên tiếng Việt gốc hoặc bản dịch người dùng đã sửa thật sự.
        if (string.Equals(existing.LanguageCode, "vi", StringComparison.OrdinalIgnoreCase))
            return false;

        // Chỉ sửa các bản dịch demo đang bị giữ nguyên tên nguồn như English/Vietnamese.
        var localizedIsDifferentFromSource =
            !string.Equals(localizedName, sourceName, StringComparison.OrdinalIgnoreCase);

        if (!localizedIsDifferentFromSource)
            return false;

        var looksLikeFallbackName =
            string.IsNullOrWhiteSpace(existingName) ||
            string.Equals(existingName, sourceName, StringComparison.OrdinalIgnoreCase) ||
            !ContainsExpectedScript(existingName, existing.LanguageCode);

        return looksLikeFallbackName;
    }

    private static bool ContainsExpectedScript(string value, string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return (languageCode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "zh" => value.Any(ch => ch >= '\u4e00' && ch <= '\u9fff'),
            "ja" => value.Any(ch =>
                (ch >= '\u3040' && ch <= '\u30ff') ||
                (ch >= '\u4e00' && ch <= '\u9fff')),
            "ko" => value.Any(ch => ch >= '\uac00' && ch <= '\ud7af'),
            _ => true
        };
    }

    private static string GetLocalizedPoiName(string languageCode, string fallbackName)
    {
        var code = (languageCode ?? string.Empty).Trim().ToLowerInvariant();
        var key = ToSafeSlug(fallbackName);

        return key switch
        {
            "dinh-doc-lap" or "independence-palace" => PickName(code, "Dinh Độc Lập", "Independence Palace", "独立宫", "独立宮殿", "독립궁", "Palais de l’Indépendance"),
            "nha-tho-duc-ba-sai-gon" or "notre-dame-cathedral-of-saigon" => PickName(code, "Nhà thờ Đức Bà Sài Gòn", "Notre Dame Cathedral of Saigon", "西贡圣母大教堂", "サイゴン大教会", "사이공 노트르담 대성당", "Cathédrale Notre-Dame de Saïgon"),
            "buu-dien-trung-tam-sai-gon" or "saigon-central-post-office" => PickName(code, "Bưu điện Trung tâm Sài Gòn", "Saigon Central Post Office", "西贡中央邮局", "サイゴン中央郵便局", "사이공 중앙우체국", "Poste centrale de Saïgon"),
            "cho-ben-thanh" or "ben-thanh-market" => PickName(code, "Chợ Bến Thành", "Ben Thanh Market", "滨城市场", "ベンタイン市場", "벤탄시장", "Marché Bến Thành"),
            "bao-tang-chung-tich-chien-tranh" or "war-remnants-museum" => PickName(code, "Bảo tàng Chứng tích Chiến tranh", "War Remnants Museum", "战争遗迹博物馆", "戦争証跡博物館", "전쟁박물관", "Musée des vestiges de guerre"),
            "nha-hat-thanh-pho-ho-chi-minh" or "nha-hat-thanh-pho" or "saigon-opera-house" => PickName(code, "Nhà hát Thành phố Hồ Chí Minh", "Saigon Opera House", "西贡歌剧院", "サイゴン・オペラハウス", "사이공 오페라 하우스", "Opéra de Saïgon"),
            "pho-di-bo-nguyen-hue" or "nguyen-hue-walking-street" => PickName(code, "Phố đi bộ Nguyễn Huệ", "Nguyen Hue Walking Street", "阮惠步行街", "グエンフエ歩行者天国", "응우옌 후에 보행자 거리", "Rue piétonne Nguyễn Huệ"),
            "bitexco-financial-tower" => PickName(code, "Bitexco Financial Tower", "Bitexco Financial Tower", "Bitexco金融塔", "ビテクスコ・フィナンシャルタワー", "비텍스코 파이낸셜 타워", "Tour financière Bitexco"),
            "bao-tang-my-thuat-tp-hcm" or "ho-chi-minh-city-fine-arts-museum" => PickName(code, "Bảo tàng Mỹ thuật TP.HCM", "Ho Chi Minh City Fine Arts Museum", "胡志明市美术馆", "ホーチミン市美術館", "호찌민시 미술관", "Musée des Beaux-Arts de Hô Chi Minh-Ville"),
            "chua-ngoc-hoang" or "jade-emperor-pagoda" => PickName(code, "Chùa Ngọc Hoàng", "Jade Emperor Pagoda", "玉皇殿", "玉皇殿", "옥황사", "Pagode de l’Empereur de Jade"),
            "thao-cam-vien-sai-gon" or "saigon-zoo-and-botanical-gardens" => PickName(code, "Thảo Cầm Viên Sài Gòn", "Saigon Zoo and Botanical Gardens", "西贡动植物园", "サイゴン動植物園", "사이공 동식물원", "Jardin botanique et zoo de Saïgon"),
            "ben-nha-rong" or "nha-rong-wharf" => PickName(code, "Bến Nhà Rồng", "Nha Rong Wharf", "芽龙码头", "ニャロン埠頭", "냐롱 부두", "Quai Nhà Rồng"),
            "chua-giac-lam" or "giac-lam-pagoda" => PickName(code, "Chùa Giác Lâm", "Giac Lam Pagoda", "觉林寺", "ザックラム寺", "지악럼 사원", "Pagode Giác Lâm"),
            "cho-binh-tay" or "binh-tay-market" => PickName(code, "Chợ Bình Tây", "Binh Tay Market", "平西市场", "ビンタイ市場", "빈떠이시장", "Marché Bình Tây"),
            "pho-di-bo-bui-vien" or "bui-vien-walking-street" => PickName(code, "Phố đi bộ Bùi Viện", "Bui Vien Walking Street", "裴援步行街", "ブイビエン通り", "부이비엔 거리", "Rue piétonne Bùi Viện"),
            "landmark-81" => PickName(code, "Landmark 81", "Landmark 81", "地标塔81", "ランドマーク81", "랜드마크 81", "Landmark 81"),
            "cau-ba-son" or "ba-son-bridge" => PickName(code, "Cầu Ba Son", "Ba Son Bridge", "巴山桥", "バーソン橋", "바선교", "Pont Ba Son"),
            "cong-vien-tao-dan" or "tao-dan-park" => PickName(code, "Công viên Tao Đàn", "Tao Dan Park", "陶丹公园", "タオダン公園", "타오단 공원", "Parc Tao Đàn"),
            "duong-sach-nguyen-van-binh" or "nguyen-van-binh-book-street" => PickName(code, "Đường sách Nguyễn Văn Bình", "Nguyen Van Binh Book Street", "阮文平书街", "グエンヴァンビン・ブックストリート", "응우옌 반 빈 책거리", "Rue du livre Nguyễn Văn Bình"),
            "bao-tang-lich-su-tp-hcm" or "ho-chi-minh-city-museum-of-history" => PickName(code, "Bảo tàng Lịch sử TP.HCM", "Ho Chi Minh City Museum of History", "胡志明市历史博物馆", "ホーチミン市歴史博物館", "호찌민시 역사박물관", "Musée d’histoire de Hô Chi Minh-Ville"),
            "banh-mi-huynh-hoa" or "banh-mi-huynh-hoa-le-thi-rieng" => PickName(code, "Bánh Mì Huynh Hoa", "Banh Mi Huynh Hoa", "黄花法棍店", "バインミー・フインホア", "반미 후인호아", "Bánh Mì Huynh Hoa"),
            _ => fallbackName
        };
    }

    private static string PickName(
        string languageCode,
        string vi,
        string en,
        string zh,
        string ja,
        string ko,
        string fr)
    {
        return languageCode switch
        {
            "vi" => vi,
            "zh" => zh,
            "ja" => ja,
            "ko" => ko,
            "fr" => fr,
            _ => en
        };
    }

    private static IReadOnlyList<DemoPoi> BuildPois() =>
    [
        .. BuildCorePois(),
        .. BuildExpandedPois()
    ];

    private static IReadOnlyList<DemoPoi> BuildCorePois() =>
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

    private static IReadOnlyList<DemoPoi> BuildExpandedPois() =>
    [
        new DemoPoi(
            "bao-tang-tphcm", "Bảo tàng Thành phố Hồ Chí Minh", "Ho Chi Minh City Museum",
            10.776600m, 106.699900m, 60, ["history", "architecture", "museum"],
            "Bảo tàng trong dinh Gia Long, giới thiệu lịch sử hình thành và phát triển của thành phố.",
            "Bảo tàng Thành phố Hồ Chí Minh nằm trong một công trình kiến trúc cổ trang nhã ở trung tâm. Các phòng trưng bày kể về địa lý, thương cảng, văn hóa đô thị, phong trào đấu tranh và quá trình phát triển của Sài Gòn - TP.HCM.",
            "A museum in the former Gia Long Palace presenting the city's history and development.",
            "Ho Chi Minh City Museum occupies an elegant heritage building in the center. Its galleries introduce local geography, port commerce, urban culture and the many stages of Saigon's development."),
        new DemoPoi(
            "nha-tho-tan-dinh", "Nhà thờ Tân Định", "Tan Dinh Church",
            10.788400m, 106.690700m, 55, ["architecture", "spiritual"],
            "Nhà thờ màu hồng nổi bật với kiến trúc duyên dáng giữa khu Tân Định.",
            "Nhà thờ Tân Định gây ấn tượng bởi sắc hồng đặc trưng, tháp chuông cao và nhiều chi tiết trang trí. Du khách thường dừng bên ngoài để chụp ảnh, đồng thời cần giữ trật tự và tôn trọng các giờ sinh hoạt tôn giáo.",
            "A striking pink church with graceful architecture in the Tan Dinh neighborhood.",
            "Tan Dinh Church is known for its pink facade, tall bell tower and decorative details. Visitors enjoy viewing it from the street while respecting worship activities and the surrounding community."),
        new DemoPoi(
            "chua-vinh-nghiem", "Chùa Vĩnh Nghiêm", "Vinh Nghiem Pagoda",
            10.790300m, 106.682300m, 65, ["spiritual", "architecture"],
            "Ngôi chùa lớn có tháp đá và không gian Phật giáo trang nghiêm gần trung tâm.",
            "Chùa Vĩnh Nghiêm kết hợp đường nét kiến trúc truyền thống với quy mô của một cơ sở Phật giáo đô thị. Khuôn viên rộng, tháp cao và các điện thờ là nơi phù hợp để tìm hiểu nghi lễ, kiến trúc và đời sống tinh thần của người dân.",
            "A major Buddhist complex with a stone tower and a calm ceremonial atmosphere.",
            "Vinh Nghiem Pagoda combines traditional architectural forms with the scale of an urban Buddhist center. Its spacious grounds, towers and worship halls offer insight into local spiritual life."),
        new DemoPoi(
            "chua-ba-thien-hau", "Chùa Bà Thiên Hậu", "Thien Hau Temple",
            10.753100m, 106.661200m, 55, ["spiritual", "history", "culture"],
            "Ngôi miếu cổ tiêu biểu của cộng đồng người Hoa tại khu Chợ Lớn.",
            "Chùa Bà Thiên Hậu nổi bật với sân trong, phù điêu gốm, nhang vòng và không khí tín ngưỡng lâu đời. Đây là điểm quan trọng để hiểu lịch sử định cư, hoạt động cộng đồng và văn hóa tâm linh của người Hoa ở Sài Gòn.",
            "A historic Chinese temple and an important spiritual landmark in Cho Lon.",
            "Thien Hau Temple features courtyards, ceramic reliefs and hanging incense coils. It offers a meaningful introduction to Chinese-Vietnamese settlement, community life and religious traditions in Saigon."),
        new DemoPoi(
            "chua-quan-am", "Chùa Quan Âm", "Quan Am Pagoda",
            10.752500m, 106.660000m, 50, ["spiritual", "culture", "architecture"],
            "Ngôi chùa người Hoa giàu màu sắc với nhiều tượng thờ và chi tiết trang trí.",
            "Chùa Quan Âm là một điểm dừng giàu chiều sâu trong tuyến Chợ Lớn. Mái ngói, tượng thờ, hoành phi và không gian hương khói phản ánh sự giao thoa giữa tín ngưỡng, nghệ thuật trang trí và đời sống cộng đồng.",
            "A colorful Chinese-Vietnamese pagoda with rich statues and ornamentation.",
            "Quan Am Pagoda is a rewarding stop in Cho Lon. Its tiled roofs, statues, inscriptions and incense-filled halls reveal the meeting of faith, decorative art and community life."),
        new DemoPoi(
            "hoi-quan-ha-chuong", "Hội quán Hà Chương", "Ha Chuong Assembly Hall",
            10.754300m, 106.665100m, 45, ["history", "architecture", "culture"],
            "Hội quán cổ của cộng đồng người Hoa với nghệ thuật chạm khắc tinh xảo.",
            "Hội quán Hà Chương lưu giữ nhiều chi tiết gỗ, đá và gốm được chế tác công phu. Không gian vừa mang chức năng tín ngưỡng vừa cho thấy vai trò của hội quán trong việc kết nối cộng đồng thương nhân và cư dân xưa.",
            "A historic Chinese assembly hall known for detailed wood, stone and ceramic work.",
            "Ha Chuong Assembly Hall preserves elaborate carvings and decorative craft. Its spaces demonstrate how assembly halls supported both worship and community connections among earlier residents and merchants."),
        new DemoPoi(
            "viet-nam-quoc-tu", "Việt Nam Quốc Tự", "Vietnam Quoc Tu Pagoda",
            10.767200m, 106.667900m, 75, ["spiritual", "architecture"],
            "Quần thể Phật giáo nổi bật với bảo tháp cao trên trục đường 3 Tháng 2.",
            "Việt Nam Quốc Tự là một công trình Phật giáo quy mô lớn giữa khu đô thị đông đúc. Bảo tháp, chính điện và sân rộng tạo nên điểm nhấn kiến trúc, đồng thời là nơi diễn ra nhiều hoạt động văn hóa và nghi lễ.",
            "A prominent Buddhist complex with a tall tower on 3 Thang 2 Street.",
            "Vietnam Quoc Tu is a large Buddhist landmark within a busy urban district. Its tower, main hall and courtyards create a strong architectural presence and host important cultural and religious activities."),
        new DemoPoi(
            "chua-xa-loi", "Chùa Xá Lợi", "Xa Loi Pagoda",
            10.775100m, 106.686000m, 55, ["spiritual", "history"],
            "Ngôi chùa thanh tịnh gắn với nhiều câu chuyện Phật giáo Việt Nam thế kỷ 20.",
            "Chùa Xá Lợi có tháp chuông dễ nhận biết và không gian yên tĩnh ngay gần trung tâm. Ngoài giá trị tôn giáo, chùa còn gợi mở nhiều câu chuyện lịch sử và văn hóa Phật giáo trong đời sống đô thị hiện đại.",
            "A tranquil pagoda connected with important twentieth-century Buddhist history.",
            "Xa Loi Pagoda has a recognizable bell tower and a peaceful atmosphere near the city center. Beyond worship, it introduces significant stories of Buddhism in modern Vietnamese urban life."),
        new DemoPoi(
            "nha-tho-cha-tam", "Nhà thờ Cha Tam", "Cha Tam Church",
            10.752300m, 106.653700m, 50, ["spiritual", "history", "architecture"],
            "Nhà thờ Công giáo mang dấu ấn giao thoa văn hóa Việt - Hoa ở Chợ Lớn.",
            "Nhà thờ Cha Tam có kiến trúc Công giáo kết hợp những chi tiết gợi nhắc văn hóa Hoa. Vị trí giữa Chợ Lớn khiến nơi đây trở thành một điểm kể chuyện thú vị về tôn giáo và sự đa dạng cộng đồng của thành phố.",
            "A Catholic church reflecting Vietnamese and Chinese cultural influences in Cho Lon.",
            "Cha Tam Church combines Catholic architecture with details associated with Chinese culture. Its Cho Lon setting makes it a compelling place to discuss faith and the city's diverse communities."),
        new DemoPoi(
            "den-mariamman", "Đền Mariamman", "Mariamman Hindu Temple",
            10.772500m, 106.695600m, 40, ["spiritual", "culture", "architecture"],
            "Ngôi đền Hindu rực rỡ nằm giữa khu thương mại trung tâm Quận 1.",
            "Đền Mariamman nổi bật với cổng tháp trang trí nhiều tượng thần đầy màu sắc. Không gian nhỏ nhưng giàu chi tiết, thể hiện một lớp văn hóa tôn giáo đặc biệt giữa trung tâm Sài Gòn; du khách nên bỏ giày và tham quan với thái độ tôn trọng.",
            "A colorful Hindu temple in the commercial heart of District 1.",
            "Mariamman Hindu Temple is recognizable by its tower covered with vivid deity figures. Its richly detailed interior reveals a distinctive religious layer of central Saigon and should be visited respectfully."),
        new DemoPoi(
            "cho-ho-thi-ky", "Chợ hoa Hồ Thị Kỷ", "Ho Thi Ky Flower Market",
            10.763300m, 106.681200m, 80, ["market", "culture", "shopping"],
            "Khu chợ hoa lâu năm với sắc màu, hương thơm và nhịp giao nhận sôi động.",
            "Chợ hoa Hồ Thị Kỷ hoạt động nhộn nhịp với nhiều loại hoa được đưa về từ các vùng trồng. Những lối nhỏ quanh chợ còn có nhiều món ăn đường phố, tạo nên trải nghiệm gần gũi về thương mại và đời sống khu dân cư.",
            "A long-running flower market filled with color, fragrance and active local trade.",
            "Ho Thi Ky Flower Market receives flowers from growing regions around Vietnam. Its narrow surrounding lanes also offer street food, creating an intimate view of neighborhood commerce and daily life."),
        new DemoPoi(
            "cho-tan-dinh", "Chợ Tân Định", "Tan Dinh Market",
            10.789000m, 106.689000m, 70, ["market", "food", "shopping"],
            "Khu chợ địa phương nổi tiếng với thực phẩm, vải vóc và mặt tiền màu vàng.",
            "Chợ Tân Định phục vụ đời sống hằng ngày của cư dân với hàng khô, thực phẩm tươi, món ăn và nhiều gian vải. Khi kết hợp với Nhà thờ Tân Định gần đó, du khách có thể cảm nhận một khu phố lâu đời vừa sôi động vừa nhiều màu sắc.",
            "A local market known for food, textiles and its yellow facade.",
            "Tan Dinh Market serves neighborhood life through fresh produce, dry goods, prepared food and textile stalls. Together with the nearby pink church, it forms a vivid and colorful local route."),
        new DemoPoi(
            "cho-an-dong", "Chợ An Đông", "An Dong Market",
            10.757800m, 106.673200m, 85, ["market", "shopping", "culture"],
            "Trung tâm mua sắm lâu đời nổi bật về quần áo, vải và hàng thời trang.",
            "Chợ An Đông là một điểm buôn bán lớn ở khu vực Quận 5, được biết đến với hàng may mặc, phụ kiện và vải vóc. Không khí nhiều tầng và mật độ gian hàng cao cho thấy quy mô thương mại của khu Chợ Lớn mở rộng.",
            "A long-established shopping center known for clothing, textiles and accessories.",
            "An Dong Market is a major trading destination in District 5. Its multi-level layout and dense fashion stalls demonstrate the commercial scale of the wider Cho Lon area."),
        new DemoPoi(
            "cong-vien-ben-bach-dang", "Công viên Bến Bạch Đằng", "Bach Dang Wharf Park",
            10.773500m, 106.706900m, 100, ["river", "park", "modern"],
            "Công viên ven sông trung tâm, lý tưởng để đi bộ và ngắm tàu thuyền.",
            "Công viên Bến Bạch Đằng mở ra tầm nhìn rộng về sông Sài Gòn và khu đô thị phía đông. Đây là nơi phù hợp để nghỉ chân, chụp ảnh skyline, quan sát tàu thuyền và bắt đầu một hành trình bằng buýt sông.",
            "A central riverside park ideal for walking, skyline views and watching boats.",
            "Bach Dang Wharf Park offers broad views across the Saigon River toward the eastern urban area. It is a pleasant place to rest, photograph the skyline and begin a river-bus journey."),
        new DemoPoi(
            "ga-tau-thuy-bach-dang", "Ga tàu thủy Bạch Đằng", "Bach Dang Waterbus Station",
            10.774100m, 106.707300m, 45, ["river", "modern"],
            "Điểm khởi hành buýt sông giúp ngắm thành phố từ một góc nhìn khác.",
            "Ga tàu thủy Bạch Đằng kết nối khu trung tâm với các điểm dọc sông Sài Gòn. Hành trình trên mặt nước mang lại góc quan sát thoáng đãng về bến cảng, cầu, cao ốc và những khu dân cư ven sông.",
            "A river-bus departure point offering a different view of the city.",
            "Bach Dang Waterbus Station connects downtown with stops along the Saigon River. Traveling on the water gives open views of wharves, bridges, towers and riverside neighborhoods."),
        new DemoPoi(
            "cong-vien-bo-song-sai-gon", "Công viên bờ sông Sài Gòn", "Saigon Riverfront Park",
            10.776900m, 106.712500m, 120, ["river", "park", "modern"],
            "Không gian công cộng ven sông phía Thủ Thiêm với tầm nhìn trực diện về trung tâm.",
            "Công viên bờ sông phía Thủ Thiêm là điểm ngắm đường chân trời trung tâm rất dễ tiếp cận. Bãi cỏ, lối đi và khoảng mở ven nước phù hợp cho buổi chiều mát, đặc biệt khi thành phố bắt đầu lên đèn.",
            "A Thu Thiem riverfront space with direct views of downtown Saigon.",
            "Saigon Riverfront Park in Thu Thiem provides an accessible downtown skyline viewpoint. Lawns, paths and open waterside space are especially pleasant in the late afternoon and evening."),
        new DemoPoi(
            "cong-vien-sala", "Công viên Sala", "Sala Park",
            10.770400m, 106.723500m, 100, ["park", "modern", "nature"],
            "Công viên đô thị mới với hồ nước, lối đi và biểu tượng hoa sala.",
            "Công viên Sala mang dáng vẻ hiện đại, thoáng đãng với các mảng xanh và mặt nước. Đây là điểm nghỉ chân phù hợp khi khám phá khu đô thị Thủ Thiêm, đồng thời cho thấy cách thành phố phát triển những không gian công cộng mới.",
            "A modern urban park with water, walking paths and a distinctive sala-flower landmark.",
            "Sala Park is a spacious contemporary green area with landscaped water and paths. It provides a relaxing stop in Thu Thiem and illustrates the development of newer public spaces."),
        new DemoPoi(
            "cong-vien-gia-dinh", "Công viên Gia Định", "Gia Dinh Park",
            10.813400m, 106.677400m, 130, ["park", "nature", "family"],
            "Mảng xanh lớn gần sân bay với nhiều lối đi và khu vui chơi trẻ em.",
            "Công viên Gia Định có thảm cỏ rộng, cây xanh lâu năm và không gian vận động cho nhiều lứa tuổi. Buổi sáng và chiều mát, nơi đây thể hiện rõ nhịp sinh hoạt thường ngày của cư dân thành phố.",
            "A large green area near the airport with paths and children's play spaces.",
            "Gia Dinh Park has broad lawns, mature trees and activity areas for different ages. In the morning and late afternoon, it offers a clear view of everyday recreational life in the city."),
        new DemoPoi(
            "cong-vien-hoang-van-thu", "Công viên Hoàng Văn Thụ", "Hoang Van Thu Park",
            10.801300m, 106.667700m, 110, ["park", "nature"],
            "Công viên xanh có hình dáng đặc trưng tại cửa ngõ sân bay Tân Sơn Nhất.",
            "Công viên Hoàng Văn Thụ là một ốc đảo xanh giữa các trục đường lớn. Hồ nước, cây cao và đường dạo tạo không gian nghỉ ngơi thuận tiện cho du khách trước hoặc sau chuyến bay.",
            "A distinctive green park near the gateway to Tan Son Nhat Airport.",
            "Hoang Van Thu Park forms a green island among major roads. Its lake, tall trees and paths provide a convenient resting place before or after a flight."),
        new DemoPoi(
            "cong-vien-le-van-tam", "Công viên Lê Văn Tám", "Le Van Tam Park",
            10.787000m, 106.693000m, 95, ["park", "culture"],
            "Công viên trung tâm nhiều bóng mát, thường có hoạt động cộng đồng và hội sách.",
            "Công viên Lê Văn Tám là điểm xanh dễ ghé trên các tuyến tham quan Quận 1 và Quận 3. Những lối đi rợp cây và khoảng sân rộng thường đón hoạt động thể thao, sự kiện văn hóa và sinh hoạt cộng đồng.",
            "A shaded central park that often hosts community activities and book fairs.",
            "Le Van Tam Park is an accessible green stop between Districts 1 and 3. Tree-lined paths and open areas regularly support exercise, cultural events and neighborhood gatherings."),
        new DemoPoi(
            "ho-con-rua", "Hồ Con Rùa", "Turtle Lake",
            10.782900m, 106.696400m, 60, ["culture", "modern", "food"],
            "Vòng xoay và không gian tụ họp quen thuộc với nhiều món ăn vặt quanh hồ.",
            "Hồ Con Rùa là một điểm hẹn đô thị gần nhiều trường học và công trình trung tâm. Buổi chiều tối, các bậc ngồi quanh hồ trở nên nhộn nhịp với người trẻ, xe hàng ăn vặt và nhịp sống đường phố.",
            "A familiar urban meeting point surrounded by popular street snacks.",
            "Turtle Lake is a city gathering place near schools and central landmarks. In the late afternoon and evening, its steps become lively with young people, snack vendors and street life."),
        new DemoPoi(
            "tru-so-ubnd-tphcm", "Trụ sở UBND Thành phố Hồ Chí Minh", "Ho Chi Minh City Hall",
            10.776500m, 106.700900m, 50, ["architecture", "history"],
            "Công trình hành chính có mặt tiền cổ điển nổi bật ở đầu phố Nguyễn Huệ.",
            "Trụ sở UBND Thành phố là một biểu tượng kiến trúc quan trọng của khu trung tâm. Công trình không mở cửa tham quan thông thường, nhưng mặt tiền và quảng trường phía trước tạo thành góc nhìn đẹp, đặc biệt khi được chiếu sáng vào buổi tối.",
            "A landmark civic building with a classical facade at the head of Nguyen Hue Street.",
            "Ho Chi Minh City Hall is a major architectural symbol downtown. Although regular interior visits are unavailable, its facade and the square in front create a memorable view, especially after dark."),
        new DemoPoi(
            "bao-tang-ao-dai", "Bảo tàng Áo dài", "Ao Dai Museum",
            10.842100m, 106.822800m, 90, ["museum", "culture", "architecture"],
            "Không gian ngoại thành kể câu chuyện áo dài Việt Nam giữa cảnh quan xanh.",
            "Bảo tàng Áo dài giới thiệu sự thay đổi của trang phục truyền thống qua nhiều giai đoạn và nhân vật. Kiến trúc gỗ, mặt nước và khu vườn giúp chuyến tham quan kết hợp nội dung văn hóa với trải nghiệm thư giãn.",
            "A green suburban museum telling the story of Vietnam's ao dai.",
            "The Ao Dai Museum presents the evolution of traditional dress through different periods and notable figures. Wooden architecture, water and gardens combine cultural learning with a relaxed visit."),
        new DemoPoi(
            "bao-tang-phu-nu-nam-bo", "Bảo tàng Phụ nữ Nam Bộ", "Southern Women's Museum",
            10.784500m, 106.688700m, 55, ["museum", "history", "culture"],
            "Bảo tàng tôn vinh vai trò, đời sống và đóng góp của phụ nữ phương Nam.",
            "Bảo tàng Phụ nữ Nam Bộ lưu giữ trang phục, công cụ, hình ảnh và tư liệu về phụ nữ trong gia đình, lao động, văn hóa và lịch sử. Nội dung giúp bổ sung những góc nhìn con người thường ít xuất hiện trong các tuyến tham quan phổ biến.",
            "A museum honoring the lives, roles and contributions of southern Vietnamese women.",
            "The Southern Women's Museum preserves clothing, tools, photographs and documents about women in family, work, culture and history. It adds valuable human perspectives to a city itinerary."),
        new DemoPoi(
            "bao-tang-chien-dich-ho-chi-minh", "Bảo tàng Chiến dịch Hồ Chí Minh", "Ho Chi Minh Campaign Museum",
            10.787100m, 106.706600m, 60, ["museum", "history"],
            "Bảo tàng chuyên đề về chiến dịch lịch sử kết thúc chiến tranh năm 1975.",
            "Bảo tàng Chiến dịch Hồ Chí Minh trưng bày bản đồ, hình ảnh, hiện vật và sa bàn liên quan đến chiến dịch. Đây là điểm dành cho du khách muốn tìm hiểu sâu hơn về lịch sử quân sự và bối cảnh của những ngày tháng 4 năm 1975.",
            "A focused museum about the historic campaign that ended the war in 1975.",
            "The Ho Chi Minh Campaign Museum displays maps, photographs, artifacts and models connected with the campaign. It suits visitors seeking deeper military history and context for April 1975."),
        new DemoPoi(
            "bao-tang-y-hoc-co-truyen", "Bảo tàng Y học Cổ truyền Việt Nam", "Museum of Traditional Vietnamese Medicine",
            10.775000m, 106.670300m, 50, ["museum", "culture", "architecture"],
            "Bảo tàng độc đáo giới thiệu dược liệu, dụng cụ và lịch sử y học cổ truyền.",
            "Bảo tàng Y học Cổ truyền có nội thất gỗ giàu chi tiết cùng bộ sưu tập dược liệu, sách và dụng cụ chữa bệnh. Không gian giúp du khách hiểu cách tri thức y học được lưu truyền và ứng dụng trong văn hóa Việt Nam.",
            "A distinctive museum of herbs, instruments and traditional Vietnamese medicine.",
            "The Museum of Traditional Vietnamese Medicine combines detailed wooden interiors with collections of herbs, books and medical tools. It explains how healing knowledge has been recorded and practiced in Vietnamese culture."),
        new DemoPoi(
            "artinus-3d-art-museum", "Bảo tàng tranh 3D Artinus", "Artinus 3D Art Museum",
            10.741200m, 106.669800m, 70, ["museum", "entertainment", "family"],
            "Không gian tranh tương tác phù hợp cho gia đình và người thích chụp ảnh sáng tạo.",
            "Artinus sử dụng tranh phối cảnh để tạo các bối cảnh vui nhộn mà du khách có thể bước vào và chụp ảnh. Đây là lựa chọn giải trí trong nhà, đặc biệt phù hợp với nhóm bạn hoặc gia đình có trẻ em.",
            "An interactive perspective-art space for families and creative photography.",
            "Artinus uses perspective paintings to create playful scenes visitors can enter and photograph. It is an indoor entertainment option well suited to groups of friends and families with children."),
        new DemoPoi(
            "ho-ban-nguyet", "Hồ Bán Nguyệt", "Crescent Lake",
            10.729200m, 106.718800m, 130, ["park", "modern", "river"],
            "Không gian hồ và lối đi bộ hiện đại, yên tĩnh ở khu đô thị Phú Mỹ Hưng.",
            "Hồ Bán Nguyệt có đường dạo rộng, cảnh quan mặt nước và nhiều tiện ích xung quanh. Không khí thoáng đãng khiến nơi đây phù hợp cho một buổi chiều thư giãn trước khi đi bộ sang Cầu Ánh Sao.",
            "A calm modern lake and promenade in the Phu My Hung urban area.",
            "Crescent Lake offers broad walking paths, waterside scenery and nearby amenities. Its open atmosphere makes it a relaxing late-afternoon stop before crossing Starlight Bridge."),
        new DemoPoi(
            "cau-anh-sao", "Cầu Ánh Sao", "Starlight Bridge",
            10.726900m, 106.718100m, 80, ["modern", "entertainment", "park"],
            "Cầu đi bộ nổi tiếng với ánh sáng trang trí trong khu Hồ Bán Nguyệt.",
            "Cầu Ánh Sao kết nối hai bờ không gian xanh quanh Hồ Bán Nguyệt. Buổi tối, hệ thống ánh sáng và mặt nước tạo nên khung cảnh phù hợp để đi dạo, chụp ảnh và kết thúc một tuyến tham quan phía nam thành phố.",
            "A popular pedestrian bridge known for decorative lighting beside Crescent Lake.",
            "Starlight Bridge links the landscaped sides of Crescent Lake. In the evening, lighting and water create an attractive setting for walking, photography and ending a southern-city route."),
        new DemoPoi(
            "cong-vien-van-hoa-dam-sen", "Công viên Văn hóa Đầm Sen", "Dam Sen Cultural Park",
            10.766300m, 106.639200m, 150, ["entertainment", "family", "park"],
            "Công viên giải trí lâu năm với trò chơi, cảnh quan và hoạt động gia đình.",
            "Đầm Sen là một địa điểm vui chơi quen thuộc của nhiều thế hệ cư dân thành phố. Khuôn viên rộng kết hợp trò chơi, hồ nước, vườn cây và các chương trình giải trí, phù hợp cho chuyến đi gia đình kéo dài nửa ngày hoặc cả ngày.",
            "A long-running amusement park with rides, landscapes and family activities.",
            "Dam Sen has been a familiar recreation destination for generations of city residents. Its large grounds combine rides, water, gardens and shows for a half-day or full family visit."),
        new DemoPoi(
            "khu-du-lich-suoi-tien", "Khu Du lịch Văn hóa Suối Tiên", "Suoi Tien Theme Park",
            10.867000m, 106.802200m, 170, ["entertainment", "family", "culture"],
            "Công viên chủ đề kết hợp trò chơi với hình ảnh văn hóa và truyền thuyết Việt Nam.",
            "Suối Tiên có quy mô lớn với nhiều trò chơi, công trình tạo hình và khu vực vui chơi dưới nước. Các chủ đề từ lịch sử, truyền thuyết và văn hóa dân gian tạo cho điểm đến một phong cách rất riêng.",
            "A theme park combining rides with Vietnamese cultural and legendary imagery.",
            "Suoi Tien is a large attraction with rides, sculptural environments and water-play areas. Themes drawn from history, legends and folklore give the park its distinctive identity."),
        new DemoPoi(
            "khu-du-tru-sinh-quyen-can-gio", "Khu dự trữ sinh quyển Cần Giờ", "Can Gio Mangrove Biosphere Reserve",
            10.411300m, 106.954700m, 250, ["nature", "river", "history"],
            "Hệ sinh thái rừng ngập mặn rộng lớn ở cửa ngõ biển của TP.HCM.",
            "Rừng ngập mặn Cần Giờ là vùng sinh thái quan trọng với mạng lưới sông rạch, cây đước và nhiều loài động vật. Chuyến đi cần nhiều thời gian hơn nội đô nhưng mang lại góc nhìn hoàn toàn khác về tự nhiên và quá trình phục hồi môi trường.",
            "An extensive mangrove ecosystem at Ho Chi Minh City's coastal gateway.",
            "Can Gio's mangroves form an important ecosystem of tidal waterways, trees and wildlife. The longer trip from downtown rewards visitors with a different perspective on nature and environmental recovery."),
        new DemoPoi(
            "dao-khi-can-gio", "Đảo Khỉ Cần Giờ", "Can Gio Monkey Island",
            10.407800m, 106.953600m, 130, ["nature", "family", "entertainment"],
            "Khu tham quan giữa rừng ngập mặn có quần thể khỉ sinh sống tự nhiên.",
            "Đảo Khỉ là điểm phổ biến trong hành trình Cần Giờ, nơi du khách quan sát khỉ trong môi trường rừng. Cần giữ chắc đồ dùng cá nhân, không trêu chọc động vật và tuân theo hướng dẫn của khu tham quan.",
            "A mangrove attraction with a large free-ranging monkey population.",
            "Monkey Island is a popular Can Gio stop where visitors observe monkeys in a forest environment. Personal belongings should be secured, animals should not be teased and site instructions must be followed."),
        new DemoPoi(
            "chien-khu-rung-sac", "Chiến khu Rừng Sác", "Sac Forest Revolutionary Base",
            10.400500m, 106.955600m, 120, ["history", "nature", "museum"],
            "Di tích lịch sử nằm giữa rừng ngập mặn, tái hiện đời sống chiến đấu gian khó.",
            "Chiến khu Rừng Sác kết hợp cảnh quan sinh thái với các mô hình và câu chuyện lịch sử. Lối đi xuyên rừng giúp du khách hình dung điều kiện tự nhiên đặc biệt và cuộc sống của lực lượng hoạt động tại đây trong chiến tranh.",
            "A historic base within the mangroves recreating difficult wartime conditions.",
            "Sac Forest Revolutionary Base combines ecological scenery with historical displays and stories. Paths through the mangroves help visitors imagine the distinctive natural conditions and wartime life in the area."),
        new DemoPoi(
            "bien-30-thang-4-can-gio", "Bãi biển 30 Tháng 4 Cần Giờ", "Can Gio 30 April Beach",
            10.390900m, 106.960900m, 180, ["nature", "river", "food"],
            "Bãi biển gần thành phố với không khí thoáng đãng và nhiều quán hải sản.",
            "Bãi biển 30 Tháng 4 là điểm dừng thư giãn trong chuyến đi Cần Giờ. Màu nước chịu ảnh hưởng phù sa, nhưng không gian rộng, gió biển và ẩm thực hải sản tạo nên trải nghiệm ngoại thành gần gũi.",
            "A nearby coastal stop with open air and local seafood restaurants.",
            "Can Gio's 30 April Beach is a relaxing stop on a coastal day trip. Sediment affects the water color, while open space, sea breezes and seafood offer an accessible escape from downtown."),
        new DemoPoi(
            "ban-dao-thanh-da", "Bán đảo Thanh Đa", "Thanh Da Peninsula",
            10.824500m, 106.720800m, 170, ["river", "nature", "culture"],
            "Bán đảo xanh ven sông với nhịp sống chậm ngay gần trung tâm thành phố.",
            "Thanh Đa có những con đường nhỏ, bờ sông, vườn cây và khu dân cư mang cảm giác tách khỏi đô thị đông đúc. Đây là nơi phù hợp để đi chậm, ngắm cảnh và tìm hiểu mối quan hệ giữa Sài Gòn với hệ thống sông nước.",
            "A green riverside peninsula with a slower rhythm close to central Saigon.",
            "Thanh Da's lanes, riverbanks, gardens and neighborhoods feel separated from the dense city. It is a place to slow down and understand Saigon's close relationship with waterways."),
        new DemoPoi(
            "khu-du-lich-binh-quoi", "Khu Du lịch Bình Quới", "Binh Quoi Tourist Village",
            10.831000m, 106.731000m, 130, ["nature", "food", "river", "family"],
            "Không gian làng quê Nam Bộ bên sông với ẩm thực và vườn cây xanh mát.",
            "Bình Quới tái hiện một phần cảnh quan nông thôn Nam Bộ qua ao nước, cầu nhỏ, hàng dừa và các khu ẩm thực. Điểm đến phù hợp cho gia đình hoặc nhóm muốn nghỉ ngơi mà không cần rời thành phố quá xa.",
            "A riverside southern-village setting with food, gardens and tropical greenery.",
            "Binh Quoi recreates elements of southern rural scenery through ponds, small bridges, palms and dining areas. It suits families and groups seeking a restful outing without traveling far from the city."),
        new DemoPoi(
            "khu-du-lich-van-thanh", "Khu Du lịch Văn Thánh", "Van Thanh Tourist Area",
            10.797700m, 106.715500m, 110, ["nature", "food", "park"],
            "Không gian xanh ven hồ nằm gần khu trung tâm và tuyến metro.",
            "Văn Thánh mang lại cảm giác nghỉ dưỡng với hồ nước, cây xanh và các khu ẩm thực ngay giữa đô thị. Đây là điểm chuyển tiếp nhẹ nhàng giữa tuyến tham quan trung tâm và khu vực Bình Thạnh - Thanh Đa.",
            "A green lakeside retreat close to downtown and the metro corridor.",
            "Van Thanh provides a resort-like atmosphere with water, trees and dining spaces within the city. It is a gentle transition between downtown sightseeing and the Binh Thanh–Thanh Da area."),
        new DemoPoi(
            "chung-cu-nguyen-thien-thuat", "Chung cư Nguyễn Thiện Thuật", "Nguyen Thien Thuat Apartment Blocks",
            10.766900m, 106.677700m, 70, ["culture", "food", "history"],
            "Khu chung cư cũ với những lối đi nhỏ và đời sống ẩm thực địa phương phong phú.",
            "Cụm chung cư Nguyễn Thiện Thuật cho thấy một lát cắt đời sống Sài Gòn qua ban công, cầu thang, hàng quán và các con hẻm. Du khách nên đi nhẹ nhàng vì đây trước hết vẫn là không gian sinh hoạt của cư dân.",
            "Older apartment blocks with narrow lanes and a rich local food scene.",
            "Nguyen Thien Thuat's apartment blocks reveal everyday Saigon through balconies, stairs, food stalls and alleys. Visitors should move quietly because this remains a residential environment."),
        new DemoPoi(
            "pho-do-co-le-cong-kieu", "Phố đồ cổ Lê Công Kiều", "Le Cong Kieu Antique Street",
            10.769100m, 106.699400m, 45, ["shopping", "history", "culture"],
            "Con phố ngắn gần chợ Bến Thành với nhiều cửa hàng đồ xưa và sưu tầm.",
            "Lê Công Kiều là một tuyến phố nhỏ nơi có thể bắt gặp đồng hồ, gốm, tượng, tiền xu và nhiều món đồ cũ. Dù mua sắm hay chỉ quan sát, du khách nên hỏi kỹ nguồn gốc, giá và thông tin sản phẩm.",
            "A short street near Ben Thanh Market lined with antiques and collectibles.",
            "Le Cong Kieu Street offers clocks, ceramics, statues, coins and many older objects. Whether browsing or buying, visitors should ask carefully about provenance, price and product details."),
        new DemoPoi(
            "khu-pho-nhat-le-thanh-ton", "Khu phố Nhật Lê Thánh Tôn", "Le Thanh Ton Japan Town",
            10.781200m, 106.704000m, 70, ["food", "culture", "modern"],
            "Mạng lưới hẻm nhỏ nổi tiếng với nhà hàng Nhật và không khí về đêm.",
            "Khu phố Nhật quanh Lê Thánh Tôn và Thái Văn Lung có nhiều nhà hàng, quán nhỏ và biển hiệu đặc trưng. Buổi tối, nơi đây thể hiện một khía cạnh quốc tế của Sài Gòn và sự đa dạng trong văn hóa ẩm thực đô thị.",
            "A network of small lanes known for Japanese restaurants and evening atmosphere.",
            "Japan Town around Le Thanh Ton and Thai Van Lung contains restaurants, compact bars and recognizable signage. At night it reflects Saigon's international character and diverse urban food culture.")
    ];


    private static IReadOnlyList<RealOwnerPoiMenuSeed> BuildRealOwnerPoiMenus() =>
    [
        new RealOwnerPoiMenuSeed(
            1,
            "banh-mi-huynh-hoa-le-thi-rieng",
            "owner_huynhhoa",
            "huynhhoa.owner@versa.local",
            "owner123",
            "Bánh Mì Huynh Hoa",
            "Quản lý Bánh Mì Huynh Hoa",
            "0338044646",
            "26-30-32 Lê Thị Riêng, Phường Bến Thành, Quận 1, TP.HCM",
            "Bánh Mì Huynh Hoa - Lê Thị Riêng",
            "Banh Mi Huynh Hoa - Le Thi Rieng",
            10.771700m,
            106.692200m,
            55,
            ["market", "culture"],
            "Tiệm bánh mì nổi tiếng tại trung tâm Quận 1, phù hợp để demo đặt món nhanh sau khi quét QR.",
            "Bánh Mì Huynh Hoa là một địa điểm ẩm thực quen thuộc của du khách khi khám phá trung tâm TP.HCM. Trong hệ thống VERSA, POI này được gắn với tài khoản chủ gian hàng riêng, có menu bánh mì, pate, bơ, chả và món ăn kèm để du khách đặt trước hoặc lưu lại khi tham quan khu vực Bến Thành - Lê Thị Riêng.",
            "A famous banh mi shop in central District 1, useful for QR-based food ordering demos.",
            "Banh Mi Huynh Hoa is a well-known food stop in central Ho Chi Minh City. In VERSA, this POI is connected to an owner account, a menu, QR discovery and demo ordering features.",
            [
                new RealMenuItemSeed("Bánh bao Huynh Hoa", "Món bánh bao trong menu Huynh Hoa.", 35000m),
                new RealMenuItemSeed("Bánh mì 2 vị Tê Bơ", "Bánh mì hai vị, giá demo lấy mức thấp của khoảng công bố.", 48000m),
                new RealMenuItemSeed("Bánh mì 3 vị Tê Bơ Bông", "Bánh mì ba vị, giá demo lấy mức thấp của khoảng công bố.", 61000m),
                new RealMenuItemSeed("Bánh mì 3 vị Tê Bơ Xá xíu", "Bánh mì ba vị có xá xíu, giá demo lấy mức thấp của khoảng công bố.", 61000m),
                new RealMenuItemSeed("Bánh mì Bơ Huynh Hoa", "Bánh mì bơ Huynh Hoa.", 28000m),
                new RealMenuItemSeed("Bánh mì Huynh Hoa truyền thống đặc biệt", "Bánh mì truyền thống đặc biệt.", 61000m),
                new RealMenuItemSeed("Bánh mì Pate Huynh Hoa", "Bánh mì pate Huynh Hoa.", 38000m),
                new RealMenuItemSeed("Bơ tươi Huynh Hoa", "Hũ bơ tươi dùng kèm bánh mì.", 50000m),
                new RealMenuItemSeed("Cơm cháy Chà Bông", "Cơm cháy chà bông đóng gói.", 65000m),
                new RealMenuItemSeed("Cơm cháy mắm", "Cơm cháy mắm đóng gói.", 50000m)
            ]),
        new RealOwnerPoiMenuSeed(
            2,
            "pho-viet-nam-tran-quoc-toan",
            "owner_phovietnam",
            "phovietnam.owner@versa.local",
            "owner123",
            "Phở Việt Nam",
            "Quản lý Phở Việt Nam",
            "02838201237",
            "66 Trần Quốc Toản, Phường 8, Quận 3, TP.HCM",
            "Phở Việt Nam - Trần Quốc Toản",
            "Pho Viet Nam - Tran Quoc Toan",
            10.787100m,
            106.688900m,
            60,
            ["market", "culture"],
            "Quán phở tại Quận 3 có menu phở, món thêm và đồ uống, phù hợp demo đặt món tại POI.",
            "Phở Việt Nam tại Trần Quốc Toản là dữ liệu POI ẩm thực gắn với chủ gian hàng trong TP.HCM. Menu trong app gồm phở thố đá, phở đặc biệt, phở tái, nạm, gân, món thêm và đồ uống để kiểm tra đầy đủ luồng: chủ POI quản lý món, du khách xem menu, tạo đơn và chủ xác nhận đơn.",
            "A District 3 pho restaurant with pho, add-ons and drinks for owner-menu demos.",
            "Pho Viet Nam on Tran Quoc Toan is seeded as a food POI with an owner account and menu items so the app can demonstrate browsing and ordering from a POI.",
            [
                new RealMenuItemSeed("Phở thố đá", "Món phở đặc trưng phục vụ trong thố đá nóng.", 100000m),
                new RealMenuItemSeed("Phở đặc biệt", "Tô phở đặc biệt.", 80000m),
                new RealMenuItemSeed("Phở đuôi", "Phở bò với phần đuôi.", 80000m),
                new RealMenuItemSeed("Phở sườn", "Phở bò với sườn.", 80000m),
                new RealMenuItemSeed("Phở tái", "Phở bò tái.", 60000m),
                new RealMenuItemSeed("Phở vè", "Phở bò phần vè.", 60000m),
                new RealMenuItemSeed("Phở nạm", "Phở bò nạm.", 60000m),
                new RealMenuItemSeed("Phở bắp hoa lõi rùa", "Phở bò bắp hoa.", 80000m),
                new RealMenuItemSeed("Phở gân", "Phở bò gân.", 60000m),
                new RealMenuItemSeed("Phở viên", "Phở bò viên.", 60000m),
                new RealMenuItemSeed("Quẩy", "Quẩy ăn kèm phở.", 3000m),
                new RealMenuItemSeed("Chén thịt thêm", "Phần thịt thêm.", 35000m),
                new RealMenuItemSeed("Dừa tươi", "Nước dừa tươi.", 30000m),
                new RealMenuItemSeed("Trà đào", "Đồ uống trà đào.", 25000m)
            ]),
        new RealOwnerPoiMenuSeed(
            3,
            "com-tam-ba-ghien-dang-van-ngu",
            "owner_baghien",
            "baghien.owner@versa.local",
            "owner123",
            "Cơm Tấm Ba Ghiền",
            "Quản lý Cơm Tấm Ba Ghiền",
            "0903000003",
            "84 Đặng Văn Ngữ, Phường 10, Phú Nhuận, TP.HCM",
            "Cơm Tấm Ba Ghiền - Đặng Văn Ngữ",
            "Com Tam Ba Ghien - Dang Van Ngu",
            10.798300m,
            106.677500m,
            65,
            ["market", "culture"],
            "Quán cơm tấm nổi tiếng ở Phú Nhuận, dùng để demo chủ POI có menu món ăn địa phương.",
            "Cơm Tấm Ba Ghiền là POI ẩm thực tại Phú Nhuận. Dữ liệu menu trong VERSA dùng để minh họa nhóm món cơm tấm, món thêm và đơn đặt món tại quầy. Giá được đặt trong khoảng tham khảo công khai để phục vụ demo, có thể thay đổi ngoài thực tế.",
            "A well-known broken rice spot in Phu Nhuan for local food menu demos.",
            "Com Tam Ba Ghien is seeded as a local food POI with an owner account, menu items and demo orders. Prices are demo references and may change in real life.",
            [
                new RealMenuItemSeed("Cơm tấm sườn", "Cơm tấm sườn nướng.", 50000m),
                new RealMenuItemSeed("Cơm tấm sườn bì", "Cơm tấm sườn nướng kèm bì.", 58000m),
                new RealMenuItemSeed("Cơm tấm sườn chả", "Cơm tấm sườn nướng kèm chả trứng.", 60000m),
                new RealMenuItemSeed("Cơm tấm sườn bì chả", "Phần cơm tấm đầy đủ phổ biến.", 66000m),
                new RealMenuItemSeed("Sườn nướng thêm", "Phần sườn nướng gọi thêm.", 40000m),
                new RealMenuItemSeed("Chả trứng", "Chả trứng ăn kèm cơm tấm.", 15000m),
                new RealMenuItemSeed("Bì heo", "Bì heo ăn kèm.", 15000m),
                new RealMenuItemSeed("Trứng ốp la", "Trứng ốp la gọi thêm.", 12000m)
            ]),
        new RealOwnerPoiMenuSeed(
            4,
            "ca-phe-do-phu-com-tam-dai-han",
            "owner_dophu",
            "dophu.owner@versa.local",
            "owner123",
            "Cà Phê Đỗ Phủ - Cơm Tấm Đại Hàn",
            "Quản lý Cà Phê Đỗ Phủ",
            "0898107113",
            "113A Đặng Dung, Phường Tân Định, Quận 1, TP.HCM",
            "Cà Phê Đỗ Phủ - Cơm Tấm Đại Hàn",
            "Do Phu Coffee - Dai Han Broken Rice",
            10.791700m,
            106.690200m,
            55,
            ["history", "market", "culture"],
            "Không gian cà phê - cơm tấm gắn với câu chuyện Biệt động Sài Gòn, phù hợp POI lịch sử kết hợp menu.",
            "Cà Phê Đỗ Phủ - Cơm Tấm Đại Hàn là POI kết hợp trải nghiệm ẩm thực và câu chuyện lịch sử. Trong app, POI này giúp demo mô hình chủ POI có thể vừa giới thiệu nội dung thuyết minh, vừa bán món ăn/uống cho du khách tại điểm tham quan.",
            "A coffee and broken-rice location connected to Saigon commando history.",
            "Do Phu Coffee is a seeded owner POI combining historical storytelling with food and drinks, suitable for demonstrating menu ordering inside a tourism app.",
            [
                new RealMenuItemSeed("Cơm tấm thường", "Phần cơm tấm cơ bản.", 35000m),
                new RealMenuItemSeed("Cơm tấm sườn bì", "Phần cơm tấm sườn bì.", 45000m),
                new RealMenuItemSeed("Cơm tấm đặc biệt ba món", "Phần đặc biệt gồm ba món chính.", 70000m),
                new RealMenuItemSeed("Canh thêm", "Phần canh gọi thêm.", 5000m),
                new RealMenuItemSeed("Đồ chua thêm", "Phần đồ chua gọi thêm.", 5000m),
                new RealMenuItemSeed("Cà phê sữa đá", "Cà phê sữa đá kiểu Việt.", 30000m),
                new RealMenuItemSeed("Cà phê đen đá", "Cà phê đen đá.", 25000m),
                new RealMenuItemSeed("Nước sâm", "Đồ uống giải khát.", 20000m)
            ]),
        new RealOwnerPoiMenuSeed(
            5,
            "pizza-4ps-ben-thanh",
            "owner_pizza4ps",
            "pizza4ps.owner@versa.local",
            "owner123",
            "Pizza 4P's",
            "Quản lý Pizza 4P's",
            "19006043",
            "Khu vực Bến Thành, Quận 1, TP.HCM",
            "Pizza 4P's - Bến Thành",
            "Pizza 4P's - Ben Thanh",
            10.773100m,
            106.698300m,
            60,
            ["modern", "market"],
            "Nhà hàng pizza tại TP.HCM, dùng để demo menu món quốc tế trong cùng hệ thống du lịch.",
            "Pizza 4P's được thêm như một POI ẩm thực hiện đại ở khu trung tâm để hệ thống có dữ liệu menu đa dạng hơn. Menu gồm pizza và món ăn theo giá công bố công khai, phục vụ demo du khách đặt món tại điểm đến.",
            "A modern pizza restaurant POI in central Ho Chi Minh City for international food menu demos.",
            "Pizza 4P's is seeded as a modern dining POI with owner, menu and order demo data for a more diverse tourism-food experience.",
            [
                new RealMenuItemSeed("Spicy Garlic Shrimp Pizza", "Pizza tôm tỏi cay.", 254000m),
                new RealMenuItemSeed("Burrata Parma Ham", "Pizza burrata và parma ham.", 298000m),
                new RealMenuItemSeed("Extra Parma Ham Margherita", "Margherita thêm parma ham.", 331000m),
                new RealMenuItemSeed("Milano Salami & Chorizo Margherita Pizza", "Pizza salami Milano và chorizo.", 238000m),
                new RealMenuItemSeed("Burrata Parma Ham Margherita", "Margherita burrata và parma ham.", 298000m),
                new RealMenuItemSeed("House-made Cheese Margherita", "Margherita phô mai nhà làm.", 198000m),
                new RealMenuItemSeed("Truffle French Fries", "Khoai tây chiên vị truffle.", 98000m),
                new RealMenuItemSeed("Green Salad", "Salad xanh ăn kèm.", 92000m)
            ])
    ];

    private sealed record RealOwnerPoiMenuSeed(
        int Order,
        string Slug,
        string Username,
        string Email,
        string Password,
        string BusinessName,
        string RepresentativeName,
        string Phone,
        string Address,
        string PoiName,
        string PoiNameEn,
        decimal Latitude,
        decimal Longitude,
        int Radius,
        IReadOnlyList<string> CategoryKeys,
        string ShortDescription,
        string FullDescription,
        string ShortDescriptionEn,
        string FullDescriptionEn,
        IReadOnlyList<RealMenuItemSeed> MenuItems);

    private sealed record RealMenuItemSeed(string Name, string Description, decimal Price);

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
