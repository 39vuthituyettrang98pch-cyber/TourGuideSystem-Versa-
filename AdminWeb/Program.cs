using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using AdminWeb.Services.MediaProcessing;
using AdminWeb.Services.Payments;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

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

if (string.IsNullOrWhiteSpace(jwtSigningKey) || jwtSigningKey.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Jwt:SigningKey must be configured with a real production secret.");

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

            // Public tourist landing page is mapped at `/`, but it still belongs to the DuKhach portal.
            // Without this, `/` is authenticated through AdminScheme, so after a tourist logs in
            // the navbar still looks anonymous and shows "Đăng nhập".
            if (!context.Request.Path.HasValue ||
                context.Request.Path == "/" ||
                context.Request.Path.StartsWithSegments("/DuKhach") ||
                context.Request.Path.StartsWithSegments("/Areas/DuKhach"))
                return "TouristScheme";

            if (context.Request.Path.StartsWithSegments("/Owner") ||
                context.Request.Path.StartsWithSegments("/Areas/Owner"))
                return "OwnerScheme";

            if (context.Request.Path.StartsWithSegments("/Editor") ||
                context.Request.Path.StartsWithSegments("/Areas/Editor"))
                return "EditorScheme";

            if (context.Request.Path.StartsWithSegments("/Reviewer") ||
                context.Request.Path.StartsWithSegments("/Areas/Reviewer"))
                return "ReviewerScheme";

            return "AdminScheme";
        };
    })
    .AddCookie("AdminScheme", options =>
    {
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "Versa.AdminAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            var path = context.Request.Path;
            var loginPath = path.StartsWithSegments("/Editor")
                ? "/Editor/Login"
                : path.StartsWithSegments("/Reviewer")
                    ? "/Reviewer/Login"
                    : "/Admin/Login";

            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            context.Response.Redirect(loginPath + "?returnUrl=" + Uri.EscapeDataString(returnUrl));
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            var path = context.Request.Path;
            var deniedPath = path.StartsWithSegments("/Editor")
                ? "/Editor/Login"
                : path.StartsWithSegments("/Reviewer")
                    ? "/Reviewer/Login"
                    : "/Admin/Login";

            context.Response.Redirect(deniedPath);
            return Task.CompletedTask;
        };
    })

    .AddCookie("EditorScheme", options =>
    {
        options.LoginPath = "/Areas/Editor/Login";
        options.LogoutPath = "/Areas/Editor/Logout";
        options.AccessDeniedPath = "/Areas/Editor/Login";
        options.Cookie.Name = "Versa.EditorAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            context.Response.Redirect("/Areas/Editor/Login?returnUrl=" + Uri.EscapeDataString(returnUrl));
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect("/Areas/Editor/Login");
            return Task.CompletedTask;
        };
    })
    .AddCookie("ReviewerScheme", options =>
    {
        options.LoginPath = "/Areas/Reviewer/Login";
        options.LogoutPath = "/Areas/Reviewer/Logout";
        options.AccessDeniedPath = "/Areas/Reviewer/Login";
        options.Cookie.Name = "Versa.ReviewerAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            context.Response.Redirect("/Areas/Reviewer/Login?returnUrl=" + Uri.EscapeDataString(returnUrl));
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect("/Areas/Reviewer/Login");
            return Task.CompletedTask;
        };
    })
    .AddCookie("TouristScheme", options =>
    {
        options.LoginPath = "/Areas/DuKhach/Account/Login";
        options.LogoutPath = "/Areas/DuKhach/Account/Logout";
        options.AccessDeniedPath = "/Areas/DuKhach/Account/Login";
        options.Cookie.Name = "Versa.TouristAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    })
    .AddCookie("OwnerScheme", options =>
    {
        options.LoginPath = "/Areas/Owner/Login";
        options.LogoutPath = "/Areas/Owner/Account/Logout";
        options.AccessDeniedPath = "/Areas/Owner/Login";
        options.Cookie.Name = "Versa.OwnerAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
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
            .RequireRole("Admin");
    });

    options.AddPolicy("TouristAreaPolicy", policy =>
    {
        policy
            .AddAuthenticationSchemes("TouristScheme")
            .RequireAuthenticatedUser()
            .RequireRole("Tourist");
    });

    options.AddPolicy("OwnerAreaPolicy", policy =>
    {
        policy
            .AddAuthenticationSchemes("OwnerScheme")
            .RequireAuthenticatedUser()
            .RequireRole("Owner");
    });

    options.AddPolicy("EditorAreaPolicy", policy =>
    {
        policy
            .AddAuthenticationSchemes("EditorScheme")
            .RequireAuthenticatedUser()
            .RequireRole("Editor");
    });

    options.AddPolicy("ReviewerAreaPolicy", policy =>
    {
        policy
            .AddAuthenticationSchemes("ReviewerScheme")
            .RequireAuthenticatedUser()
            .RequireRole("Reviewer");
    });
});

// Lightweight protection for public AI endpoints so Gemini/OpenAI quota is not burned by spam.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AiPerIp", httpContext =>
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ip = string.IsNullOrWhiteSpace(forwardedFor)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : forwardedFor.Split(',')[0].Trim();

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 12,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("PasswordResetPerIp", httpContext =>
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ip = string.IsNullOrWhiteSpace(forwardedFor)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : forwardedFor.Split(',')[0].Trim();

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

// Core services
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<PasswordResetService>();

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
builder.Services.AddScoped<TouristAudioQuotaService>();
builder.Services.AddSingleton<VnPayPaymentService>();
builder.Services.AddHttpClient<MomoPaymentService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<PaymentActivationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;

    if (httpContext.Response.HasStarted)
        return;

    var statusCode = httpContext.Response.StatusCode;
    var originalPath = httpContext.Request.Path.Value ?? string.Empty;

    if (originalPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
    {
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.WriteAsync($"{{\"statusCode\":{statusCode},\"message\":\"Request failed.\"}}");
        return;
    }

    var targetPath = originalPath.StartsWith("/Areas/Owner", StringComparison.OrdinalIgnoreCase) ||
                     originalPath.StartsWith("/Owner", StringComparison.OrdinalIgnoreCase)
        ? $"/Areas/Owner/Error/HttpStatus?code={statusCode}"
        : originalPath.StartsWith("/Areas/Editor", StringComparison.OrdinalIgnoreCase) ||
          originalPath.StartsWith("/Editor", StringComparison.OrdinalIgnoreCase)
            ? $"/Areas/Editor/Error/HttpStatus?code={statusCode}"
            : originalPath.StartsWith("/Areas/Reviewer", StringComparison.OrdinalIgnoreCase) ||
              originalPath.StartsWith("/Reviewer", StringComparison.OrdinalIgnoreCase)
                ? $"/Areas/Reviewer/Error/HttpStatus?code={statusCode}"
                : originalPath.StartsWith("/Areas/DuKhach", StringComparison.OrdinalIgnoreCase) ||
                  originalPath.StartsWith("/DuKhach", StringComparison.OrdinalIgnoreCase)
                    ? $"/Areas/DuKhach/Error/HttpStatus?code={statusCode}"
                    : $"/Home/HttpStatus?code={statusCode}";

    httpContext.Response.Redirect(targetPath);
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();


// Some old links/forms can inherit the current staff area (for example /Editor/MenuOrders/Create)
// while the feature actually belongs to the DuKhach portal. Rewrite these paths before routing
// so GET and POST order requests keep their body and use the Tourist cookie scheme correctly.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    static bool IsWrongStaffTouristPath(string value, string prefix)
    {
        return value.StartsWith($"/{prefix}/PoiMenu", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith($"/{prefix}/MenuOrders", StringComparison.OrdinalIgnoreCase);
    }

    if (IsWrongStaffTouristPath(path, "Editor")
        || IsWrongStaffTouristPath(path, "Admin")
        || IsWrongStaffTouristPath(path, "Reviewer")
        || IsWrongStaffTouristPath(path, "Areas/Editor")
        || IsWrongStaffTouristPath(path, "Areas/Admin")
        || IsWrongStaffTouristPath(path, "Areas/Reviewer"))
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var featureIndex = Array.FindIndex(segments, segment =>
            segment.Equals("PoiMenu", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("MenuOrders", StringComparison.OrdinalIgnoreCase));

        if (featureIndex >= 0)
            context.Request.Path = "/Areas/DuKhach/" + string.Join('/', segments.Skip(featureIndex));
    }

    await next();
});

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();

// Legacy URL guard only.
// Mỗi portal dùng cookie riêng nên KHÔNG được redirect Editor/Reviewer về Admin
// chỉ vì trình duyệt đang có sẵn cookie Admin. Nếu không, khi vào /Areas/Editor
// hoặc /Areas/Reviewer sau khi đăng nhập Admin sẽ bị nhảy sai về /Admin.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    if (path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    // Chỉ giữ lại redirect URL cũ /Admin/Owner/... về đúng Owner portal.
    if (path.StartsWithSegments("/Admin/Owner", out var ownerRest))
    {
        var target = string.IsNullOrWhiteSpace(ownerRest.Value)
            ? "/Areas/Owner"
            : "/Areas/Owner" + ownerRest.Value;

        context.Response.Redirect(target + context.Request.QueryString);
        return;
    }

    await next();
});

app.UseAuthorization();

// Attribute-routed API controllers: /api/poi, /api/auth, /api/ai-chat, ...
app.MapControllers();

// -----------------------------------------------------------------------------
// Route order is important.
// Admin is NOT an MVC Area in this project. It is a friendly URL alias for the
// root controllers in /Controllers. Therefore the /Admin alias must be mapped
// BEFORE routes such as /DuKhach/{controller}/{action}. Otherwise Razor tag
// helpers inside Admin pages can generate wrong links like /DuKhach/User/Edit/1.
// -----------------------------------------------------------------------------

// Public tourist landing page at `/`.
app.MapControllerRoute(
    name: "public-root",
    pattern: "",
    defaults: new { area = "DuKhach", controller = "Home", action = "Index" });

// Separate staff login portals. Each role has its own action, view and cookie scheme.
app.MapControllerRoute(
    name: "admin-login",
    pattern: "Admin/Login",
    defaults: new { area = "", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "admin-account-login",
    pattern: "Admin/Account/Login",
    defaults: new { area = "", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "admin-account-register-legacy",
    pattern: "Admin/Account/Register",
    defaults: new { area = "", controller = "Account", action = "Register" });

app.MapControllerRoute(
    name: "editor-login-short",
    pattern: "Editor/Login",
    defaults: new { area = "Editor", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "editor-account-login",
    pattern: "Editor/Account/Login",
    defaults: new { area = "Editor", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "reviewer-login-short",
    pattern: "Reviewer/Login",
    defaults: new { area = "Reviewer", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "reviewer-account-login",
    pattern: "Reviewer/Account/Login",
    defaults: new { area = "Reviewer", controller = "Account", action = "Login" });




// -----------------------------------------------------------------------------
// Area-prefixed URLs requested by the project.
// These are REAL URL routes with the word /Areas in the browser:
//   /Areas/DuKhach/...
//   /Areas/Owner/...
//   /Areas/Editor/...
//   /Areas/Reviewer/...
//
// The old friendly URLs (/DuKhach, /Owner, /Editor, /Reviewer) remain below for
// backward compatibility, but Razor link generation will prefer these routes.
// -----------------------------------------------------------------------------

app.MapControllerRoute(
    name: "areas-editor-login-short",
    pattern: "Areas/Editor/Login",
    defaults: new { area = "Editor", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "areas-editor-account-login",
    pattern: "Areas/Editor/Account/Login",
    defaults: new { area = "Editor", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "areas-reviewer-login-short",
    pattern: "Areas/Reviewer/Login",
    defaults: new { area = "Reviewer", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "areas-reviewer-account-login",
    pattern: "Areas/Reviewer/Account/Login",
    defaults: new { area = "Reviewer", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "areas-owner-login-short",
    pattern: "Areas/Owner/Login",
    defaults: new { area = "Owner", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "areas-owner-register-short",
    pattern: "Areas/Owner/Register",
    defaults: new { area = "Owner", controller = "Account", action = "Register" });

app.MapControllerRoute(
    name: "areas-payment-return-router-editor-vnpay",
    pattern: "Areas/Editor/Payments/VnPayReturn",
    defaults: new { area = "", controller = "PaymentReturnRouter", action = "VnPayReturn" });

app.MapControllerRoute(
    name: "areas-payment-return-router-reviewer-vnpay",
    pattern: "Areas/Reviewer/Payments/VnPayReturn",
    defaults: new { area = "", controller = "PaymentReturnRouter", action = "VnPayReturn" });

app.MapControllerRoute(
    name: "areas-payment-router-editor-all",
    pattern: "Areas/Editor/Payments/{action=Index}/{id?}",
    defaults: new { area = "", controller = "PaymentReturnRouter" });

app.MapControllerRoute(
    name: "areas-payment-router-reviewer-all",
    pattern: "Areas/Reviewer/Payments/{action=Index}/{id?}",
    defaults: new { area = "", controller = "PaymentReturnRouter" });

app.MapControllerRoute(
    name: "areas-editor-root",
    pattern: "Areas/Editor",
    defaults: new { area = "Editor", controller = "Dashboard", action = "Index" });

app.MapControllerRoute(
    name: "areas-editor-approval-prefix-redirect",
    pattern: "Areas/Editor/Approval/{*path}",
    defaults: new { area = "", controller = "Account", action = "RedirectEditorApprovalToCorrectPortal" });

app.MapAreaControllerRoute(
    name: "areas-editor-area-explicit",
    areaName: "Editor",
    pattern: "Areas/Editor/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { });

app.MapControllerRoute(
    name: "areas-reviewer-root",
    pattern: "Areas/Reviewer",
    defaults: new { area = "Reviewer", controller = "Dashboard", action = "Index" });

app.MapAreaControllerRoute(
    name: "areas-reviewer-area-explicit",
    areaName: "Reviewer",
    pattern: "Areas/Reviewer/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { });

app.MapControllerRoute(
    name: "areas-owner-root",
    pattern: "Areas/Owner",
    defaults: new { area = "Owner", controller = "Dashboard", action = "Index" });

app.MapAreaControllerRoute(
    name: "areas-owner-area-explicit",
    areaName: "Owner",
    pattern: "Areas/Owner/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { });

app.MapControllerRoute(
    name: "areas-dukhach-root",
    pattern: "Areas/DuKhach",
    defaults: new { area = "DuKhach", controller = "Home", action = "Index" });

app.MapAreaControllerRoute(
    name: "areas-dukhach-area-explicit",
    areaName: "DuKhach",
    pattern: "Areas/DuKhach/{controller=Home}/{action=Index}/{id?}",
    defaults: new { });


// Safety routes for VNPay return/cancel URLs that were generated by stale cookies or old portal paths.
app.MapControllerRoute(
    name: "payment-return-router-editor-vnpay",
    pattern: "Editor/Payments/VnPayReturn",
    defaults: new { area = "", controller = "PaymentReturnRouter", action = "VnPayReturn" });

app.MapControllerRoute(
    name: "payment-return-router-admin-vnpay",
    pattern: "Admin/Payments/VnPayReturn",
    defaults: new { area = "", controller = "PaymentReturnRouter", action = "VnPayReturn" });

app.MapControllerRoute(
    name: "payment-return-router-reviewer-vnpay",
    pattern: "Reviewer/Payments/VnPayReturn",
    defaults: new { area = "", controller = "PaymentReturnRouter", action = "VnPayReturn" });

// Safety routes for stale/wrong payment URLs from another portal.
// Example: a DuKhach MoMo demo form generated earlier as /Editor/Payments/ConfirmMomoDemo.
app.MapControllerRoute(
    name: "payment-router-editor-all",
    pattern: "Editor/Payments/{action=Index}/{id?}",
    defaults: new { area = "", controller = "PaymentReturnRouter" });

app.MapControllerRoute(
    name: "payment-router-admin-all",
    pattern: "Admin/Payments/{action=Index}/{id?}",
    defaults: new { area = "", controller = "PaymentReturnRouter" });

app.MapControllerRoute(
    name: "payment-router-reviewer-all",
    pattern: "Reviewer/Payments/{action=Index}/{id?}",
    defaults: new { area = "", controller = "PaymentReturnRouter" });

// Editor portal routes must be explicit and placed before Owner routes.
// Otherwise conventional link generation can accidentally produce /Owner/Workspace/...
// for Editor actions such as SubmitForReview.
app.MapControllerRoute(
    name: "editor-root",
    pattern: "Editor",
    defaults: new { area = "Editor", controller = "Dashboard", action = "Index" });

// Safety redirect for stale/bad URLs such as /Editor/Approval/PoiPending.
// Approval belongs to Reviewer (/Reviewer/Approval/...) or Admin (/Admin/Approval/...),
// not Editor. This prevents users from landing on a misleading 404 with Admin layout.
app.MapControllerRoute(
    name: "editor-approval-prefix-redirect",
    pattern: "Editor/Approval/{*path}",
    defaults: new { area = "", controller = "Account", action = "RedirectEditorApprovalToCorrectPortal" });

// Editor area routes use real wrapper controllers in Areas/Editor/Controllers/ContentControllers.cs.
// Do NOT map /Editor/Poi to root controllers with area=""; that makes Admin tag helpers
// generate /Editor/... and breaks Admin POST actions like adding POI.
app.MapAreaControllerRoute(
    name: "editor-area-explicit",
    areaName: "Editor",
    pattern: "Editor/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { });

app.MapControllerRoute(
    name: "reviewer-root",
    pattern: "Reviewer",
    defaults: new { area = "Reviewer", controller = "Dashboard", action = "Index" });

app.MapAreaControllerRoute(
    name: "reviewer-area-explicit",
    areaName: "Reviewer",
    pattern: "Reviewer/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { });

// Owner portal must be mapped explicitly before Admin/default routes so tag helpers
// never generate links such as /Admin/Owner/... or fall back to the admin layout.
app.MapControllerRoute(
    name: "owner-legacy-admin-prefix-redirect",
    pattern: "Admin/Owner/{*path}",
    defaults: new { area = "", controller = "Account", action = "RedirectAdminOwnerToOwner" });

app.MapControllerRoute(
    name: "owner-login-short",
    pattern: "Owner/Login",
    defaults: new { area = "Owner", controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "owner-register-short",
    pattern: "Owner/Register",
    defaults: new { area = "Owner", controller = "Account", action = "Register" });

app.MapControllerRoute(
    name: "owner-root",
    pattern: "Owner",
    defaults: new { area = "Owner", controller = "Dashboard", action = "Index" });

app.MapControllerRoute(
    name: "owner-map",
    pattern: "Owner/Map/{action=Index}/{id?}",
    defaults: new { area = "Owner", controller = "Map" });

// Explicit Owner POI routes to prevent link generation or stale URLs from falling back to Admin/other areas.
app.MapControllerRoute(
    name: "owner-poi-create",
    pattern: "Owner/Poi/Create",
    defaults: new { area = "Owner", controller = "Poi", action = "Create" });

app.MapControllerRoute(
    name: "owner-poi-claim",
    pattern: "Owner/Poi/Claim",
    defaults: new { area = "Owner", controller = "Poi", action = "Claim" });

app.MapControllerRoute(
    name: "owner-poi-requests",
    pattern: "Owner/Poi/Requests",
    defaults: new { area = "Owner", controller = "Poi", action = "Requests" });

app.MapControllerRoute(
    name: "owner-menu-items",
    pattern: "Owner/MenuItems/{action=Index}/{id?}",
    defaults: new { area = "Owner", controller = "MenuItems" });

app.MapControllerRoute(
    name: "owner-menu-orders",
    pattern: "Owner/MenuOrders/{action=Index}/{id?}",
    defaults: new { area = "Owner", controller = "MenuOrders" });

app.MapControllerRoute(
    name: "owner-reports",
    pattern: "Owner/Reports/{action=Index}/{id?}",
    defaults: new { area = "Owner", controller = "Reports" });

app.MapControllerRoute(
    name: "owner-reviews",
    pattern: "Owner/Reviews/{action=Index}/{id?}",
    defaults: new { area = "Owner", controller = "Reviews" });

app.MapControllerRoute(
    name: "owner-payments",
    pattern: "Owner/Payments/{action=Index}/{id?}",
    defaults: new { area = "Owner", controller = "Payments" });

app.MapControllerRoute(
    name: "owner-profile",
    pattern: "Owner/Profile/{action=Index}/{id?}",
    defaults: new { area = "Owner", controller = "Profile" });

app.MapAreaControllerRoute(
    name: "owner-area-explicit",
    areaName: "Owner",
    pattern: "Owner/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { });

// Admin workspace alias for root controllers.
app.MapControllerRoute(
    name: "admin-alias",
    pattern: "Admin/{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "" })
    .RequireAuthorization("AdminAreaPolicy");

// Tourist web routes.
app.MapControllerRoute(
    name: "dukhach-root",
    pattern: "DuKhach",
    defaults: new { area = "DuKhach", controller = "Home", action = "Index" });

app.MapAreaControllerRoute(
    name: "dukhach",
    areaName: "DuKhach",
    pattern: "DuKhach/{controller=Home}/{action=Index}/{id?}",
    defaults: new { });

// Real MVC areas: Editor, Reviewer, Owner, DuKhach. This must stay after the
// Admin alias because there is no physical Admin area.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Legacy root admin URLs such as /User, /Poi, /Tour.
app.MapControllerRoute(
    name: "admin-default",
    pattern: "{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "" })
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



    TryExecuteSql(dbContext, "ALTER TABLE pois ADD COLUMN OwnerProfileId INTEGER NULL;");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS owner_profiles (
            Id INTEGER NOT NULL CONSTRAINT PK_owner_profiles PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            BusinessName TEXT NOT NULL,
            RepresentativeName TEXT NULL,
            Phone TEXT NULL,
            Address TEXT NULL,
            Status TEXT NOT NULL DEFAULT 'Pending',
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_owner_profiles_users_UserId FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE
        );
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE UNIQUE INDEX IF NOT EXISTS IX_owner_profiles_UserId ON owner_profiles (UserId);
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS payment_plans (
            Id INTEGER NOT NULL CONSTRAINT PK_payment_plans PRIMARY KEY AUTOINCREMENT,
            PlanCode TEXT NOT NULL,
            PlanName TEXT NOT NULL,
            Audience TEXT NOT NULL DEFAULT 'Owner',
            Price TEXT NOT NULL DEFAULT '0',
            DurationDays INTEGER NOT NULL DEFAULT 30,
            Description TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1
        );
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE UNIQUE INDEX IF NOT EXISTS IX_payment_plans_PlanCode ON payment_plans (PlanCode);
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS payment_transactions (
            Id INTEGER NOT NULL CONSTRAINT PK_payment_transactions PRIMARY KEY AUTOINCREMENT,
            TransactionCode TEXT NOT NULL,
            PayerType TEXT NOT NULL DEFAULT 'Owner',
            OwnerProfileId INTEGER NULL,
            TouristId INTEGER NULL,
            PaymentPlanId INTEGER NULL,
            Purpose TEXT NOT NULL DEFAULT 'Subscription',
            Amount TEXT NOT NULL DEFAULT '0',
            Currency TEXT NOT NULL DEFAULT 'VND',
            PaymentMethod TEXT NOT NULL DEFAULT 'Manual',
            Status TEXT NOT NULL DEFAULT 'Pending',
            Note TEXT NULL,
            CreatedAt TEXT NOT NULL,
            PaidAt TEXT NULL,
            CheckoutUrl TEXT NULL,
            GatewayOrderCode TEXT NULL,
            GatewayPaymentLinkId TEXT NULL,
            GatewayStatus TEXT NULL,
            CONSTRAINT FK_payment_transactions_owner_profiles_OwnerProfileId FOREIGN KEY (OwnerProfileId) REFERENCES owner_profiles (Id) ON DELETE SET NULL,
            CONSTRAINT FK_payment_transactions_tourists_TouristId FOREIGN KEY (TouristId) REFERENCES tourists (Id) ON DELETE SET NULL,
            CONSTRAINT FK_payment_transactions_payment_plans_PaymentPlanId FOREIGN KEY (PaymentPlanId) REFERENCES payment_plans (Id) ON DELETE SET NULL
        );
    ");

    TryExecuteSql(dbContext, "ALTER TABLE payment_transactions ADD COLUMN CheckoutUrl TEXT NULL;");
    TryExecuteSql(dbContext, "ALTER TABLE payment_transactions ADD COLUMN GatewayOrderCode TEXT NULL;");
    TryExecuteSql(dbContext, "ALTER TABLE payment_transactions ADD COLUMN GatewayPaymentLinkId TEXT NULL;");
    TryExecuteSql(dbContext, "ALTER TABLE payment_transactions ADD COLUMN GatewayStatus TEXT NULL;");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE UNIQUE INDEX IF NOT EXISTS IX_payment_transactions_TransactionCode ON payment_transactions (TransactionCode);
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS owner_subscriptions (
            Id INTEGER NOT NULL CONSTRAINT PK_owner_subscriptions PRIMARY KEY AUTOINCREMENT,
            OwnerProfileId INTEGER NOT NULL,
            PaymentPlanId INTEGER NOT NULL,
            PaymentTransactionId INTEGER NULL,
            Status TEXT NOT NULL DEFAULT 'Active',
            StartsAt TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            CONSTRAINT FK_owner_subscriptions_owner_profiles_OwnerProfileId FOREIGN KEY (OwnerProfileId) REFERENCES owner_profiles (Id) ON DELETE CASCADE,
            CONSTRAINT FK_owner_subscriptions_payment_plans_PaymentPlanId FOREIGN KEY (PaymentPlanId) REFERENCES payment_plans (Id),
            CONSTRAINT FK_owner_subscriptions_payment_transactions_PaymentTransactionId FOREIGN KEY (PaymentTransactionId) REFERENCES payment_transactions (Id) ON DELETE SET NULL
        );
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS poi_owner_requests (
            Id INTEGER NOT NULL CONSTRAINT PK_poi_owner_requests PRIMARY KEY AUTOINCREMENT,
            OwnerProfileId INTEGER NOT NULL,
            PoiId INTEGER NULL,
            RequestType TEXT NOT NULL DEFAULT 'Claim',
            Status TEXT NOT NULL DEFAULT 'Pending',
            Note TEXT NULL,
            CreatedAt TEXT NOT NULL,
            ReviewedAt TEXT NULL,
            CONSTRAINT FK_poi_owner_requests_owner_profiles_OwnerProfileId FOREIGN KEY (OwnerProfileId) REFERENCES owner_profiles (Id) ON DELETE CASCADE,
            CONSTRAINT FK_poi_owner_requests_pois_PoiId FOREIGN KEY (PoiId) REFERENCES pois (Id) ON DELETE SET NULL
        );
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS tourist_subscriptions (
            Id INTEGER NOT NULL CONSTRAINT PK_tourist_subscriptions PRIMARY KEY AUTOINCREMENT,
            TouristId INTEGER NOT NULL,
            PaymentPlanId INTEGER NOT NULL,
            PaymentTransactionId INTEGER NULL,
            Status TEXT NOT NULL DEFAULT 'Active',
            StartsAt TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            CONSTRAINT FK_tourist_subscriptions_tourists_TouristId FOREIGN KEY (TouristId) REFERENCES tourists (Id) ON DELETE CASCADE,
            CONSTRAINT FK_tourist_subscriptions_payment_plans_PaymentPlanId FOREIGN KEY (PaymentPlanId) REFERENCES payment_plans (Id),
            CONSTRAINT FK_tourist_subscriptions_payment_transactions_PaymentTransactionId FOREIGN KEY (PaymentTransactionId) REFERENCES payment_transactions (Id) ON DELETE SET NULL
        );
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS IX_tourist_subscriptions_TouristId_Status
            ON tourist_subscriptions (TouristId, Status);
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS owner_menu_items (
            Id INTEGER NOT NULL CONSTRAINT PK_owner_menu_items PRIMARY KEY AUTOINCREMENT,
            OwnerProfileId INTEGER NOT NULL,
            PoiId INTEGER NOT NULL,
            Name TEXT NOT NULL,
            Description TEXT NULL,
            Price TEXT NOT NULL DEFAULT '0',
            Currency TEXT NOT NULL DEFAULT 'VND',
            ImageUrl TEXT NULL,
            Status TEXT NOT NULL DEFAULT 'Active',
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            CONSTRAINT FK_owner_menu_items_owner_profiles_OwnerProfileId FOREIGN KEY (OwnerProfileId) REFERENCES owner_profiles (Id) ON DELETE CASCADE,
            CONSTRAINT FK_owner_menu_items_pois_PoiId FOREIGN KEY (PoiId) REFERENCES pois (Id) ON DELETE CASCADE
        );
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS IX_owner_menu_items_OwnerProfileId_PoiId
            ON owner_menu_items (OwnerProfileId, PoiId);
    ");



    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS menu_orders (
            Id INTEGER NOT NULL CONSTRAINT PK_menu_orders PRIMARY KEY AUTOINCREMENT,
            OrderCode TEXT NOT NULL,
            TouristId INTEGER NOT NULL,
            OwnerProfileId INTEGER NOT NULL,
            PoiId INTEGER NOT NULL,
            CustomerName TEXT NOT NULL,
            CustomerPhone TEXT NOT NULL,
            Note TEXT NULL,
            Status TEXT NOT NULL DEFAULT 'Pending',
            PaymentMethod TEXT NOT NULL DEFAULT 'PayAtCounter',
            PaymentStatus TEXT NOT NULL DEFAULT 'Unpaid',
            Subtotal TEXT NOT NULL DEFAULT '0',
            TotalAmount TEXT NOT NULL DEFAULT '0',
            Currency TEXT NOT NULL DEFAULT 'VND',
            CreatedAt TEXT NOT NULL,
            ConfirmedAt TEXT NULL,
            CompletedAt TEXT NULL,
            CancelledAt TEXT NULL,
            CONSTRAINT FK_menu_orders_tourists_TouristId FOREIGN KEY (TouristId) REFERENCES tourists (Id) ON DELETE CASCADE,
            CONSTRAINT FK_menu_orders_owner_profiles_OwnerProfileId FOREIGN KEY (OwnerProfileId) REFERENCES owner_profiles (Id) ON DELETE CASCADE,
            CONSTRAINT FK_menu_orders_pois_PoiId FOREIGN KEY (PoiId) REFERENCES pois (Id) ON DELETE CASCADE
        );
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE UNIQUE INDEX IF NOT EXISTS IX_menu_orders_OrderCode
            ON menu_orders (OrderCode);
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS IX_menu_orders_TouristId_CreatedAt
            ON menu_orders (TouristId, CreatedAt);
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS IX_menu_orders_OwnerProfileId_Status
            ON menu_orders (OwnerProfileId, Status);
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS menu_order_items (
            Id INTEGER NOT NULL CONSTRAINT PK_menu_order_items PRIMARY KEY AUTOINCREMENT,
            MenuOrderId INTEGER NOT NULL,
            OwnerMenuItemId INTEGER NOT NULL,
            ItemName TEXT NOT NULL,
            UnitPrice TEXT NOT NULL DEFAULT '0',
            Quantity INTEGER NOT NULL DEFAULT 1,
            LineTotal TEXT NOT NULL DEFAULT '0',
            Currency TEXT NOT NULL DEFAULT 'VND',
            CONSTRAINT FK_menu_order_items_menu_orders_MenuOrderId FOREIGN KEY (MenuOrderId) REFERENCES menu_orders (Id) ON DELETE CASCADE,
            CONSTRAINT FK_menu_order_items_owner_menu_items_OwnerMenuItemId FOREIGN KEY (OwnerMenuItemId) REFERENCES owner_menu_items (Id) ON DELETE RESTRICT
        );
    ");

    dbContext.Database.ExecuteSqlRaw(@"
        CREATE INDEX IF NOT EXISTS IX_menu_order_items_MenuOrderId
            ON menu_order_items (MenuOrderId);
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



    var ownerRole = dbContext.Roles.FirstOrDefault(r => r.RoleName == "Owner");

    if (ownerRole == null)
    {
        ownerRole = new Role
        {
            RoleName = "Owner",
            Description = "Chủ gian hàng / chủ POI"
        };

        dbContext.Roles.Add(ownerRole);
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

    if (!dbContext.PaymentPlans.Any())
    {
        dbContext.PaymentPlans.AddRange(
            new PaymentPlan { PlanCode = "OWNER_FREE", PlanName = "Gói chủ gian hàng miễn phí", Audience = "Owner", Price = 0, DurationDays = 30, Description = "1 POI cơ bản, dùng để thử nghiệm.", IsActive = true },
            new PaymentPlan { PlanCode = "OWNER_BASIC", PlanName = "Gói chủ gian hàng cơ bản", Audience = "Owner", Price = 99000, DurationDays = 30, Description = "Quản lý POI, xem đánh giá, thống kê lượt nghe/quét QR.", IsActive = true },
            new PaymentPlan { PlanCode = "OWNER_PREMIUM", PlanName = "Gói nổi bật cho chủ gian hàng", Audience = "Owner", Price = 199000, DurationDays = 30, Description = "AI tối ưu nội dung, ưu tiên hiển thị, thêm media và thống kê nâng cao.", IsActive = true },
            new PaymentPlan { PlanCode = "USER_PREMIUM", PlanName = "Gói du khách premium", Audience = "Tourist", Price = 49000, DurationDays = 30, Description = "Nghe thuyết minh không giới hạn, mở tour premium và hỏi AI hướng dẫn viên.", IsActive = true }
        );
        dbContext.SaveChanges();
    }

    var touristPremiumPlan = dbContext.PaymentPlans.FirstOrDefault(plan => plan.PlanCode == "USER_PREMIUM");
    if (touristPremiumPlan != null && touristPremiumPlan.Description != "Nghe thuyết minh không giới hạn, mở tour premium và hỏi AI hướng dẫn viên.")
    {
        touristPremiumPlan.PlanName = "Gói du khách premium";
        touristPremiumPlan.Description = "Nghe thuyết minh không giới hạn, mở tour premium và hỏi AI hướng dẫn viên.";
        touristPremiumPlan.Price = touristPremiumPlan.Price <= 0 ? 49000 : touristPremiumPlan.Price;
        touristPremiumPlan.DurationDays = touristPremiumPlan.DurationDays <= 0 ? 30 : touristPremiumPlan.DurationDays;
        touristPremiumPlan.IsActive = true;
        dbContext.SaveChanges();
    }

    var ownerFeaturedPlan = dbContext.PaymentPlans.FirstOrDefault(plan => plan.PlanCode == "OWNER_PREMIUM")
        ?? dbContext.PaymentPlans.FirstOrDefault(plan => plan.PlanCode == "OWNER_FEATURED");

    if (ownerFeaturedPlan == null)
    {
        dbContext.PaymentPlans.Add(new PaymentPlan
        {
            PlanCode = "OWNER_PREMIUM",
            PlanName = "Gói nổi bật chủ POI",
            Audience = "Owner",
            Price = 199000,
            DurationDays = 30,
            Description = "AI tối ưu nội dung, upload ảnh, báo cáo nâng cao, nổi bật marker trên bản đồ du khách và ghim lên đầu khu Khám phá bản đồ.",
            IsActive = true
        });
        dbContext.SaveChanges();
    }
    else
    {
        var desiredDescription = "AI tối ưu nội dung, upload ảnh, báo cáo nâng cao, nổi bật marker trên bản đồ du khách và ghim lên đầu khu Khám phá bản đồ.";
        var changed = false;

        if (ownerFeaturedPlan.PlanName != "Gói nổi bật chủ POI")
        {
            ownerFeaturedPlan.PlanName = "Gói nổi bật chủ POI";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(ownerFeaturedPlan.Description) || !ownerFeaturedPlan.Description.Contains("bản đồ du khách", StringComparison.OrdinalIgnoreCase))
        {
            ownerFeaturedPlan.Description = desiredDescription;
            changed = true;
        }

        if (ownerFeaturedPlan.Price <= 0)
        {
            ownerFeaturedPlan.Price = 199000;
            changed = true;
        }

        if (ownerFeaturedPlan.DurationDays <= 0)
        {
            ownerFeaturedPlan.DurationDays = 30;
            changed = true;
        }

        if (!ownerFeaturedPlan.IsActive)
        {
            ownerFeaturedPlan.IsActive = true;
            changed = true;
        }

        if (changed)
            dbContext.SaveChanges();
    }

    var demoOwnerUser = dbContext.Users.FirstOrDefault(user => user.Username == "owner" || user.Email == "owner@local");
    if (demoOwnerUser == null && app.Environment.IsDevelopment())
    {
        demoOwnerUser = new User
        {
            Username = "owner",
            Email = "owner@local",
            PasswordHash = passwordService.Hash("owner123"),
            RoleId = ownerRole.Id,
            Status = "active",
            CreatedAt = DateTime.Now
        };
        dbContext.Users.Add(demoOwnerUser);
        dbContext.SaveChanges();
    }

    if (demoOwnerUser != null && !dbContext.OwnerProfiles.Any(owner => owner.UserId == demoOwnerUser.Id))
    {
        dbContext.OwnerProfiles.Add(new OwnerProfile
        {
            UserId = demoOwnerUser.Id,
            BusinessName = "Gian hàng Demo VERSA",
            RepresentativeName = "Chủ gian hàng Demo",
            Phone = "0900000000",
            Address = "Quận 1, TP.HCM",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }

    var demoOwner = demoOwnerUser == null ? null : dbContext.OwnerProfiles.FirstOrDefault(owner => owner.UserId == demoOwnerUser.Id);
    if (demoOwner != null && !dbContext.Pois.Any(poi => poi.OwnerProfileId == demoOwner.Id))
    {
        var firstPois = dbContext.Pois.OrderBy(poi => poi.Id).Take(3).ToList();
        foreach (var poi in firstPois)
        {
            poi.OwnerProfileId = demoOwner.Id;
        }
        dbContext.SaveChanges();
    }

    if (demoOwner != null && !dbContext.OwnerMenuItems.Any(item => item.OwnerProfileId == demoOwner.Id))
    {
        var demoPoi = dbContext.Pois.OrderBy(poi => poi.Id).FirstOrDefault(poi => poi.OwnerProfileId == demoOwner.Id);
        if (demoPoi != null)
        {
            dbContext.OwnerMenuItems.AddRange(
                new OwnerMenuItem
                {
                    OwnerProfileId = demoOwner.Id,
                    PoiId = demoPoi.Id,
                    Name = "Combo trải nghiệm địa phương",
                    Description = "Gói giới thiệu sản phẩm/dịch vụ nổi bật cho du khách sau khi quét QR.",
                    Price = 99000,
                    Currency = "VND",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new OwnerMenuItem
                {
                    OwnerProfileId = demoOwner.Id,
                    PoiId = demoPoi.Id,
                    Name = "Ưu đãi check-in",
                    Description = "Ưu đãi demo dành cho du khách đã check-in tại POI.",
                    Price = 49000,
                    Currency = "VND",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            dbContext.SaveChanges();
        }
    }


}


static void TryExecuteSql(AppDbContext dbContext, string sql)
{
    try
    {
        dbContext.Database.ExecuteSqlRaw(sql);
    }
    catch
    {
        // SQLite không hỗ trợ IF NOT EXISTS cho ALTER TABLE ở mọi phiên bản.
        // Nếu cột đã tồn tại thì bỏ qua để app vẫn chạy trên DB cũ/mới.
    }
}

app.Run();
