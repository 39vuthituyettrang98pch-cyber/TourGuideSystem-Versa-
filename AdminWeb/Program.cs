using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using AdminWeb.Services.MediaProcessing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        options.AreaViewLocationFormats.Add("/Views/{1}/{0}.cshtml");
        options.AreaViewLocationFormats.Add("/Views/Shared/{0}.cshtml");
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Database
var configuredConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=he_thong_thuyet_minh.db";

var sqliteConnection = new SqliteConnectionStringBuilder(configuredConnection);

if (!Path.IsPathRooted(sqliteConnection.DataSource))
{
    sqliteConnection.DataSource = Path.Combine(
        builder.Environment.ContentRootPath,
        sqliteConnection.DataSource);
}

var sqliteDirectory = Path.GetDirectoryName(sqliteConnection.DataSource);
if (!string.IsNullOrWhiteSpace(sqliteDirectory))
{
    Directory.CreateDirectory(sqliteDirectory);
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(sqliteConnection.ConnectionString));

// JWT
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];

if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Jwt:SigningKey must be configured outside Development.");

    jwtSigningKey = "TourGuideSystem-Development-Jwt-Signing-Key-Change-Me";
    builder.Configuration["Jwt:SigningKey"] = jwtSigningKey;
}

// Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "VersaSmartScheme";
        options.DefaultChallengeScheme = "VersaSmartScheme";
    })
    .AddPolicyScheme("VersaSmartScheme", "VERSA Smart Auth", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();

            if (!string.IsNullOrWhiteSpace(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            if (context.Request.Path.StartsWithSegments("/api"))
                return JwtBearerDefaults.AuthenticationScheme;

            if (context.Request.Path.StartsWithSegments("/DuKhach"))
                return "TouristScheme";

            return "AdminScheme";
        };
    })
    .AddCookie("AdminScheme", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "Versa.AdminAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
    .AddCookie("TouristScheme", options =>
    {
        options.LoginPath = "/DuKhach/Account/Login";
        options.LogoutPath = "/DuKhach/Account/Logout";
        options.AccessDeniedPath = "/DuKhach/Account/Login";
        options.Cookie.Name = "Versa.TouristAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminAreaPolicy", policy =>
    {
        policy
            .AddAuthenticationSchemes("AdminScheme")
            .RequireAuthenticatedUser()
            .RequireRole("Admin", "Editor", "Reviewer");
    });

    options.AddPolicy("TouristAreaPolicy", policy =>
    {
        policy
            .AddAuthenticationSchemes("TouristScheme")
            .RequireAuthenticatedUser()
            .RequireRole("Tourist");
    });
});

// Core services
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// Gemini AI Advisor
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddScoped<IGeminiService, GeminiService>();

// Persistent SQLite worker for AI translation, TTS, and video dubbing
builder.Services.AddHttpClient<IMultilingualMediaProcessor, MultilingualMediaProcessor>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});

if (builder.Configuration.GetValue("MediaProcessing:WorkerEnabled", true))
{
    builder.Services.AddHostedService<MediaTaskBackgroundService>();
}

builder.Services.AddScoped<SyncPackageService>();
builder.Services.AddScoped<VisitorAchievementService>();
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddScoped<ContentTranslationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .RequireAuthorization("AdminAreaPolicy");

// Seed roles + admin account
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();

    dbContext.Database.Migrate();

    // Safety net for existing local/hosting SQLite files that were created
    // before the password reset migration was added. EF migration will create
    // this table for clean databases; this SQL only prevents old DBs from
    // crashing on ForgotPassword.
    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS password_reset_tokens (
            Id INTEGER NOT NULL CONSTRAINT PK_password_reset_tokens PRIMARY KEY AUTOINCREMENT,
            TouristId INTEGER NOT NULL,
            Email TEXT NOT NULL,
            TokenHash TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            UsedAt TEXT NULL,
            CONSTRAINT FK_password_reset_tokens_tourists_TouristId
                FOREIGN KEY (TouristId) REFERENCES tourists (Id) ON DELETE CASCADE
        );
    ");
    dbContext.Database.ExecuteSqlRaw(@"
        CREATE UNIQUE INDEX IF NOT EXISTS IX_password_reset_tokens_TokenHash
            ON password_reset_tokens (TokenHash);
    ");
    dbContext.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS IX_password_reset_tokens_TouristId_ExpiresAt
            ON password_reset_tokens (TouristId, ExpiresAt);
    ");

    var poisWithoutQr = dbContext.Pois
        .Where(poi => poi.QrCodeToken == null || poi.QrCodeToken.Trim() == "")
        .ToList();

    foreach (var poi in poisWithoutQr)
    {
        poi.QrCodeToken = Guid.NewGuid().ToString("N");
    }

    if (poisWithoutQr.Count > 0)
    {
        dbContext.SaveChanges();
    }

    var adminRole = dbContext.Roles.FirstOrDefault(r => r.RoleName == "Admin");

    if (adminRole == null)
    {
        adminRole = new Role
        {
            RoleName = "Admin",
            Description = "Quản trị viên hệ thống"
        };

        dbContext.Roles.Add(adminRole);
        dbContext.SaveChanges();
    }

    var editorRole = dbContext.Roles.FirstOrDefault(r => r.RoleName == "Editor");

    if (editorRole == null)
    {
        editorRole = new Role
        {
            RoleName = "Editor",
            Description = "Biên tập viên nội dung"
        };

        dbContext.Roles.Add(editorRole);
        dbContext.SaveChanges();
    }

    var reviewerRole = dbContext.Roles.FirstOrDefault(r => r.RoleName == "Reviewer");

    if (reviewerRole == null)
    {
        reviewerRole = new Role
        {
            RoleName = "Reviewer",
            Description = "Kiểm duyệt viên nội dung"
        };

        dbContext.Roles.Add(reviewerRole);
        dbContext.SaveChanges();
    }

    var bootstrapAdminUsername = builder.Configuration["BootstrapAdmin:Username"]?.Trim();
    var bootstrapAdminEmail = builder.Configuration["BootstrapAdmin:Email"]?.Trim();
    var bootstrapAdminPassword = builder.Configuration["BootstrapAdmin:Password"];

    var adminUser = dbContext.Users.FirstOrDefault(u =>
        u.RoleId == adminRole.Id ||
        u.Username == "admin" ||
        u.Email == "admin@local");

    if (adminUser == null &&
        (app.Environment.IsDevelopment() || !string.IsNullOrWhiteSpace(bootstrapAdminPassword)))
    {
        dbContext.Users.Add(new User
        {
            Username = string.IsNullOrWhiteSpace(bootstrapAdminUsername)
                ? "admin"
                : bootstrapAdminUsername,

            Email = string.IsNullOrWhiteSpace(bootstrapAdminEmail)
                ? "admin@local"
                : bootstrapAdminEmail,

            PasswordHash = passwordService.Hash(
                app.Environment.IsDevelopment()
                    ? "admin123"
                    : bootstrapAdminPassword!),

            RoleId = adminRole.Id,
            Status = "active",
            CreatedAt = DateTime.Now
        });

        dbContext.SaveChanges();
    }

    var editorUser = dbContext.Users.FirstOrDefault(u =>
        u.Username == "editor" ||
        u.Email == "editor@local");

    if (editorUser == null && app.Environment.IsDevelopment())
    {
        dbContext.Users.Add(new User
        {
            Username = "editor",
            Email = "editor@local",
            PasswordHash = passwordService.Hash("editor123"),
            RoleId = editorRole.Id,
            Status = "active",
            CreatedAt = DateTime.Now
        });

        dbContext.SaveChanges();
    }

    var reviewerUser = dbContext.Users.FirstOrDefault(u =>
        u.Username == "reviewer" ||
        u.Email == "reviewer@local");

    if (reviewerUser == null && app.Environment.IsDevelopment())
    {
        dbContext.Users.Add(new User
        {
            Username = "reviewer",
            Email = "reviewer@local",
            PasswordHash = passwordService.Hash("reviewer123"),
            RoleId = reviewerRole.Id,
            Status = "active",
            CreatedAt = DateTime.Now
        });

        dbContext.SaveChanges();
    }

    var demoDataSeeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    demoDataSeeder.SeedAsync().GetAwaiter().GetResult();

}

app.Run();