using AdminWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Core
    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Tourist> Tourists { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    // Data
    public DbSet<SupportedLanguage> SupportedLanguages { get; set; }
    public DbSet<Category> Categories { get; set; }

    public DbSet<CategoryTranslation> CategoryTranslations { get; set; }
    public DbSet<Poi> Pois { get; set; }
    public DbSet<PoiTranslation> PoiTranslations { get; set; }
    public DbSet<PoiCategory> PoiCategories { get; set; }
    public DbSet<MediaAsset> MediaAssets { get; set; }
    public DbSet<Beacon> Beacons { get; set; }

    // Tours
    public DbSet<Tour> Tours { get; set; }
    public DbSet<TourTranslation> TourTranslations { get; set; }
    public DbSet<TourPoi> TourPois { get; set; }

    // Interactions & Logs
    public DbSet<Review> Reviews { get; set; }
    public DbSet<TouristFavorite> TouristFavorites { get; set; }
    public DbSet<VisitorPlaybackLog> VisitorPlaybackLogs { get; set; }
    public DbSet<TouristPoiDiscovery> TouristPoiDiscoveries { get; set; }

    // Media tasks
    public DbSet<MediaTask> MediaTasks { get; set; }

    // System

    public DbSet<AdminActivityLog> AdminActivityLogs { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<SyncVersion> SyncVersions { get; set; }
    public DbSet<PoiReview> PoiReviews { get; set; }
    public DbSet<TouristBookmark> TouristBookmarks { get; set; }

    // Owner / Business / Payments
    public DbSet<OwnerProfile> OwnerProfiles { get; set; }
    public DbSet<PaymentPlan> PaymentPlans { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public DbSet<OwnerSubscription> OwnerSubscriptions { get; set; }
    public DbSet<PoiOwnerRequest> PoiOwnerRequests { get; set; }
    public DbSet<TouristSubscription> TouristSubscriptions { get; set; }
    public DbSet<OwnerMenuItem> OwnerMenuItems { get; set; }
    public DbSet<MenuOrder> MenuOrders { get; set; }
    public DbSet<MenuOrderItem> MenuOrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Setup Table Names
        modelBuilder.Entity<Role>().ToTable("roles");
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Tourist>().ToTable("tourists");
        modelBuilder.Entity<PasswordResetToken>().ToTable("password_reset_tokens");
        modelBuilder.Entity<Category>().ToTable("categories");
        modelBuilder.Entity<CategoryTranslation>().ToTable("category_translations");
        modelBuilder.Entity<Poi>().ToTable("pois");
        modelBuilder.Entity<PoiTranslation>().ToTable("poi_translations");
        modelBuilder.Entity<SupportedLanguage>().ToTable("supported_languages");
        modelBuilder.Entity<MediaTask>().ToTable("media_tasks");

        modelBuilder.Entity<PoiCategory>().ToTable("poi_categories");
        modelBuilder.Entity<MediaAsset>().ToTable("media_assets");
        modelBuilder.Entity<Beacon>().ToTable("beacons");
        modelBuilder.Entity<Tour>().ToTable("tours");
        modelBuilder.Entity<TourTranslation>().ToTable("tour_translations");
        modelBuilder.Entity<TourPoi>().ToTable("tour_pois");
        modelBuilder.Entity<Review>().ToTable("reviews");
        modelBuilder.Entity<TouristFavorite>().ToTable("tourist_favorites");
        modelBuilder.Entity<VisitorPlaybackLog>().ToTable("visitor_playback_logs");
        modelBuilder.Entity<TouristPoiDiscovery>().ToTable("tourist_poi_discoveries");
        modelBuilder.Entity<AdminActivityLog>().ToTable("admin_activity_logs");
        modelBuilder.Entity<SystemSetting>().ToTable("system_settings");
        modelBuilder.Entity<SyncVersion>().ToTable("sync_versions");
        modelBuilder.Entity<PoiReview>().ToTable("poi_reviews");
        modelBuilder.Entity<TouristBookmark>().ToTable("tourist_bookmarks");
        modelBuilder.Entity<OwnerProfile>().ToTable("owner_profiles");
        modelBuilder.Entity<PaymentPlan>().ToTable("payment_plans");
        modelBuilder.Entity<PaymentTransaction>().ToTable("payment_transactions");
        modelBuilder.Entity<OwnerSubscription>().ToTable("owner_subscriptions");
        modelBuilder.Entity<PoiOwnerRequest>().ToTable("poi_owner_requests");
        modelBuilder.Entity<TouristSubscription>().ToTable("tourist_subscriptions");
        modelBuilder.Entity<OwnerMenuItem>().ToTable("owner_menu_items");
        modelBuilder.Entity<MenuOrder>().ToTable("menu_orders");
        modelBuilder.Entity<MenuOrderItem>().ToTable("menu_order_items");


        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Email)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(item => item.TokenHash)
                .HasMaxLength(128)
                .IsRequired();

            entity.HasIndex(item => item.TokenHash)
                .IsUnique();

            entity.HasIndex(item => new { item.TouristId, item.ExpiresAt });

            entity.HasOne(item => item.Tourist)
                .WithMany()
                .HasForeignKey(item => item.TouristId)
                .OnDelete(DeleteBehavior.Cascade);
        });



        modelBuilder.Entity<OwnerProfile>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.BusinessName)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(item => item.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(item => item.UserId)
                .IsUnique();

            entity.HasOne(item => item.User)
                .WithOne(user => user.OwnerProfile)
                .HasForeignKey<OwnerProfile>(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Poi>()
            .HasOne(poi => poi.OwnerProfile)
            .WithMany(owner => owner.Pois)
            .HasForeignKey(poi => poi.OwnerProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PaymentPlan>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.PlanCode)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(item => item.PlanName)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(item => item.Audience)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(item => item.Price)
                .HasColumnType("decimal(18,2)");

            entity.HasIndex(item => item.PlanCode)
                .IsUnique();
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.TransactionCode)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(item => item.PayerType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(item => item.Purpose)
                .HasMaxLength(60)
                .IsRequired();

            entity.Property(item => item.Amount)
                .HasColumnType("decimal(18,2)");

            entity.Property(item => item.Currency)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(item => item.PaymentMethod)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(item => item.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(item => item.TransactionCode)
                .IsUnique();

            entity.HasOne(item => item.OwnerProfile)
                .WithMany()
                .HasForeignKey(item => item.OwnerProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(item => item.Tourist)
                .WithMany()
                .HasForeignKey(item => item.TouristId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(item => item.PaymentPlan)
                .WithMany(plan => plan.Payments)
                .HasForeignKey(item => item.PaymentPlanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OwnerSubscription>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(item => new { item.OwnerProfileId, item.Status });

            entity.HasOne(item => item.OwnerProfile)
                .WithMany(owner => owner.Subscriptions)
                .HasForeignKey(item => item.OwnerProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.PaymentPlan)
                .WithMany(plan => plan.OwnerSubscriptions)
                .HasForeignKey(item => item.PaymentPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PaymentTransaction)
                .WithMany()
                .HasForeignKey(item => item.PaymentTransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PoiOwnerRequest>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.RequestType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(item => item.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasOne(item => item.OwnerProfile)
                .WithMany()
                .HasForeignKey(item => item.OwnerProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Poi)
                .WithMany()
                .HasForeignKey(item => item.PoiId)
                .OnDelete(DeleteBehavior.SetNull);
        });


        modelBuilder.Entity<TouristSubscription>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(item => new { item.TouristId, item.Status });

            entity.HasOne(item => item.Tourist)
                .WithMany(tourist => tourist.Subscriptions)
                .HasForeignKey(item => item.TouristId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.PaymentPlan)
                .WithMany()
                .HasForeignKey(item => item.PaymentPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PaymentTransaction)
                .WithMany()
                .HasForeignKey(item => item.PaymentTransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OwnerMenuItem>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Name)
                .HasMaxLength(180)
                .IsRequired();

            entity.Property(item => item.Price)
                .HasColumnType("decimal(18,2)");

            entity.Property(item => item.Currency)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(item => item.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(item => new { item.OwnerProfileId, item.PoiId });

            entity.HasOne(item => item.OwnerProfile)
                .WithMany(owner => owner.MenuItems)
                .HasForeignKey(item => item.OwnerProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Poi)
                .WithMany()
                .HasForeignKey(item => item.PoiId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<MenuOrder>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.OrderCode)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(item => item.CustomerName)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(item => item.CustomerPhone)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(item => item.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(item => item.PaymentMethod)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(item => item.PaymentStatus)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(item => item.Subtotal)
                .HasColumnType("decimal(18,2)");

            entity.Property(item => item.TotalAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(item => item.Currency)
                .HasMaxLength(10)
                .IsRequired();

            entity.HasIndex(item => item.OrderCode)
                .IsUnique();

            entity.HasIndex(item => new { item.TouristId, item.CreatedAt });
            entity.HasIndex(item => new { item.OwnerProfileId, item.Status });
            entity.HasIndex(item => new { item.PoiId, item.CreatedAt });

            entity.HasOne(item => item.Tourist)
                .WithMany(tourist => tourist.MenuOrders)
                .HasForeignKey(item => item.TouristId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.OwnerProfile)
                .WithMany(owner => owner.MenuOrders)
                .HasForeignKey(item => item.OwnerProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Poi)
                .WithMany(poi => poi.MenuOrders)
                .HasForeignKey(item => item.PoiId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuOrderItem>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.ItemName)
                .HasMaxLength(180)
                .IsRequired();

            entity.Property(item => item.UnitPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(item => item.LineTotal)
                .HasColumnType("decimal(18,2)");

            entity.Property(item => item.Currency)
                .HasMaxLength(10)
                .IsRequired();

            entity.HasOne(item => item.MenuOrder)
                .WithMany(order => order.Items)
                .HasForeignKey(item => item.MenuOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.OwnerMenuItem)
                .WithMany(menu => menu.OrderItems)
                .HasForeignKey(item => item.OwnerMenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Composite Keys
        modelBuilder.Entity<PoiCategory>()
            .HasKey(pc => new { pc.PoiId, pc.CategoryId });

        modelBuilder.Entity<TourPoi>()
            .HasKey(tp => new { tp.TourId, tp.PoiId });

        modelBuilder.Entity<TouristFavorite>()
            .HasKey(tf => new { tf.TouristId, tf.TargetType, tf.TargetId });

        modelBuilder.Entity<TouristPoiDiscovery>()
            .HasKey(item => new { item.TouristId, item.PoiId });

        // Setup Decimal precision for coordinates
        modelBuilder.Entity<Poi>()
            .Property(p => p.Latitude).HasColumnType("decimal(10,8)");
        modelBuilder.Entity<Poi>()
            .Property(p => p.Longitude).HasColumnType("decimal(11,8)");

        modelBuilder.Entity<VisitorPlaybackLog>()
            .Property(v => v.VisitorLatitude).HasColumnType("decimal(10,8)");
        modelBuilder.Entity<VisitorPlaybackLog>()
            .Property(v => v.VisitorLongitude).HasColumnType("decimal(11,8)");
        modelBuilder.Entity<TouristPoiDiscovery>()
            .Property(v => v.VisitorLatitude).HasColumnType("decimal(10,8)");
        modelBuilder.Entity<TouristPoiDiscovery>()
            .Property(v => v.VisitorLongitude).HasColumnType("decimal(11,8)");

        // Index/constraints
        modelBuilder.Entity<PoiTranslation>()
            .HasIndex(pt => new { pt.PoiId, pt.LanguageCode })
            .IsUnique();

        modelBuilder.Entity<CategoryTranslation>()
            .HasIndex(item => new { item.CategoryId, item.LanguageCode })
            .IsUnique();

        modelBuilder.Entity<TourTranslation>()
            .HasIndex(item => new { item.TourId, item.LanguageCode })
            .IsUnique();

        modelBuilder.Entity<SystemSetting>()
            .HasIndex(item => item.SettingKey)
            .IsUnique();

        modelBuilder.Entity<SyncVersion>()
            .HasIndex(item => item.VersionNumber)
            .IsUnique();

        modelBuilder.Entity<TouristPoiDiscovery>(entity =>
        {
            entity.Property(item => item.DiscoveryMethod)
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(item => new { item.TouristId, item.DiscoveredAt });

            entity.HasOne(item => item.Tourist)
                .WithMany(tourist => tourist.PoiDiscoveries)
                .HasForeignKey(item => item.TouristId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Poi)
                .WithMany(poi => poi.Discoveries)
                .HasForeignKey(item => item.PoiId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PoiTranslation>(entity =>
        {
            entity.Property(pt => pt.LanguageCode)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(pt => pt.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(pt => pt.ShortDescription)
                .HasMaxLength(500);

            entity.Property(pt => pt.AudioUrl)
                .HasMaxLength(2048);

            entity.Property(pt => pt.VideoUrl)
                .HasMaxLength(2048);
        });

        modelBuilder.Entity<SupportedLanguage>(entity =>
        {
            entity.HasKey(language => language.LanguageCode);

            entity.Property(language => language.LanguageCode)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(language => language.LanguageName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(language => language.EdgeTtsVoice)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasData(
                new SupportedLanguage
                {
                    LanguageCode = "vi",
                    LanguageName = "Tiếng Việt",
                    EdgeTtsVoice = "vi-VN-HoaiMyNeural",
                    IsActive = true
                },
                new SupportedLanguage
                {
                    LanguageCode = "en",
                    LanguageName = "English",
                    EdgeTtsVoice = "en-US-AriaNeural",
                    IsActive = true
                },
                new SupportedLanguage
                {
                    LanguageCode = "zh",
                    LanguageName = "中文",
                    EdgeTtsVoice = "zh-CN-XiaoxiaoNeural",
                    IsActive = true
                },
                new SupportedLanguage
                {
                    LanguageCode = "ja",
                    LanguageName = "Japanese",
                    EdgeTtsVoice = "ja-JP-NanamiNeural",
                    IsActive = true
                },
                new SupportedLanguage
                {
                    LanguageCode = "ko",
                    LanguageName = "Korean",
                    EdgeTtsVoice = "ko-KR-SunHiNeural",
                    IsActive = true
                });
        });

        modelBuilder.Entity<MediaTask>(entity =>
        {
            entity.HasKey(task => task.Id);

            entity.Property(task => task.TaskType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(task => task.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(task => task.ProgressPercentage)
                .HasDefaultValue(0);

            entity.Property(task => task.LastError)
                .HasMaxLength(4000);

            entity.HasIndex(task => new { task.Status, task.CreatedAt });

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_media_tasks_progress_percentage",
                "\"ProgressPercentage\" >= 0 AND \"ProgressPercentage\" <= 100"));

            entity.HasOne(task => task.Poi)
                .WithMany(poi => poi.MediaTasks)
                .HasForeignKey(task => task.PoiId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
