using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services.MediaProcessing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        // Editor Area reuses the established content-management views.
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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(sqliteConnection.ConnectionString));

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Jwt:SigningKey must be configured outside Development.");

    jwtSigningKey = "TourGuideSystem-Development-Jwt-Signing-Key-Change-Me";
    builder.Configuration["Jwt:SigningKey"] = jwtSigningKey;
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/DuKhach"))
            {
                var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                context.Response.Redirect($"/DuKhach/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/DuKhach"))
            {
                context.Response.Redirect("/DuKhach/Account/Login");
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }
    };
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

builder.Services.AddAuthorization();
builder.Services.AddSingleton<AdminWeb.Services.PasswordService>();
builder.Services.AddSingleton<AdminWeb.Services.JwtTokenService>();

// Gemini AI Advisor
builder.Services.AddHttpClient<AdminWeb.Services.GeminiService>();
builder.Services.AddScoped<AdminWeb.Services.IGeminiService, AdminWeb.Services.GeminiService>();

// Persistent SQLite worker for AI translation, TTS, and video dubbing
builder.Services.AddHttpClient<IMultilingualMediaProcessor, MultilingualMediaProcessor>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
if (builder.Configuration.GetValue("MediaProcessing:WorkerEnabled", true))
    builder.Services.AddHostedService<MediaTaskBackgroundService>();
builder.Services.AddScoped<AdminWeb.Services.SyncPackageService>();
builder.Services.AddScoped<AdminWeb.Services.VisitorAchievementService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed roles + admin account
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<AdminWeb.Services.PasswordService>();
    dbContext.Database.Migrate();

    var poisWithoutQr = dbContext.Pois
        .Where(poi => poi.QrCodeToken == null || poi.QrCodeToken.Trim() == "")
        .ToList();
    foreach (var poi in poisWithoutQr)
        poi.QrCodeToken = Guid.NewGuid().ToString("N");
    if (poisWithoutQr.Count > 0)
        dbContext.SaveChanges();

    var adminRole = dbContext.Roles.FirstOrDefault(r => r.RoleName == "Admin");
    if (adminRole == null)
    {
        adminRole = new Role { RoleName = "Admin", Description = "Quản trị viên hệ thống" };
        dbContext.Roles.Add(adminRole);
        dbContext.SaveChanges();
    }

    var editorRole = dbContext.Roles.FirstOrDefault(r => r.RoleName == "Editor");
    if (editorRole == null)
    {
        editorRole = new Role { RoleName = "Editor", Description = "Biên tập viên nội dung" };
        dbContext.Roles.Add(editorRole);
        dbContext.SaveChanges();
    }

    var reviewerRole = dbContext.Roles.FirstOrDefault(r => r.RoleName == "Reviewer");
    if (reviewerRole == null)
    {
        reviewerRole = new Role { RoleName = "Reviewer", Description = "Kiểm duyệt viên nội dung" };
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
        (app.Environment.IsDevelopment() ||
         !string.IsNullOrWhiteSpace(bootstrapAdminPassword)))
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

    var editorUser = dbContext.Users.FirstOrDefault(u => u.Username == "editor" || u.Email == "editor@local");
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

    var reviewerUser = dbContext.Users.FirstOrDefault(u => u.Username == "reviewer" || u.Email == "reviewer@local");
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
}

app.Run();
