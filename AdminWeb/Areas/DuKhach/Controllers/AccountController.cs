using AdminWeb.Areas.DuKhach.Models;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
public sealed class AccountController : Controller
{
    private const string TouristScheme = "TouristScheme";

    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly VisitorAchievementService _achievementService;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _environment;

    public AccountController(
        AppDbContext context,
        PasswordService passwordService,
        VisitorAchievementService achievementService,
        IEmailSender emailSender,
        IWebHostEnvironment environment)
    {
        _context = context;
        _passwordService = passwordService;
        _achievementService = achievementService;
        _emailSender = emailSender;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (IsSignedInTourist())
            return RedirectToSafeReturnUrl(returnUrl);

        return View(new DuKhachRegisterViewModel
        {
            ReturnUrl = GetSafeDuKhachReturnUrl(returnUrl)
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        DuKhachRegisterViewModel model,
        CancellationToken cancellationToken)
    {
        model.ReturnUrl = GetSafeDuKhachReturnUrl(model.ReturnUrl);

        if (!ModelState.IsValid)
            return View(model);

        var email = NormalizeEmail(model.Email);

        var emailExists = await _context.Tourists
            .AnyAsync(item => item.Email == email, cancellationToken);

        if (emailExists)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Email này đã được đăng ký. Bạn có thể đăng nhập bằng email này trên cả web và app.");

            return View(model);
        }

        var tourist = new Tourist
        {
            Email = email,
            FullName = model.FullName.Trim(),
            PasswordHash = _passwordService.Hash(model.Password),
            AuthProvider = "local",
            CreatedAt = DateTime.UtcNow
        };

        _context.Tourists.Add(tourist);
        await _context.SaveChangesAsync(cancellationToken);

        await SignInTouristAsync(tourist, isPersistent: true);

        TempData["DuKhachSuccessMessage"] = "Đăng ký thành công. Tài khoản này có thể đăng nhập trên app du lịch.";
        return RedirectToSafeReturnUrl(model.ReturnUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (IsSignedInTourist())
            return RedirectToSafeReturnUrl(returnUrl);

        return View(new DuKhachLoginViewModel
        {
            ReturnUrl = GetSafeDuKhachReturnUrl(returnUrl)
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        DuKhachLoginViewModel model,
        CancellationToken cancellationToken)
    {
        model.ReturnUrl = GetSafeDuKhachReturnUrl(model.ReturnUrl);

        if (!ModelState.IsValid)
            return View(model);

        var email = NormalizeEmail(model.Email);

        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Email == email, cancellationToken);

        if (tourist == null ||
            !_passwordService.Verify(model.Password, tourist.PasswordHash, out var needsUpgrade))
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        if (needsUpgrade)
        {
            tourist.PasswordHash = _passwordService.Hash(model.Password);
            await _context.SaveChangesAsync(cancellationToken);
        }

        await SignInTouristAsync(tourist, model.RememberMe);

        TempData["DuKhachSuccessMessage"] = "Đăng nhập du khách thành công.";
        return RedirectToSafeReturnUrl(model.ReturnUrl);
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();

        var tourist = await _context.Tourists
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);

        if (tourist == null)
            return RedirectToAction(nameof(LogoutByGet));

        var details = await _achievementService.GetDetailsAsync(touristId, cancellationToken);

        return View(new DuKhachProfileViewModel
        {
            Id = tourist.Id,
            Email = tourist.Email ?? "",
            FullName = tourist.FullName ?? "",
            CreatedAt = tourist.CreatedAt,
            TotalPoints = details?.TotalPoints ?? 0,
            DiscoveredPoiCount = details?.DiscoveredPoiCount ?? 0,
            TotalPoiCount = details?.TotalPoiCount ?? 0,
            CompletionPercentage = details?.CompletionPercentage ?? 0,
            RankName = details?.RankName ?? "Tân binh"
        });
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(
        DuKhachProfileViewModel model,
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();

        if (!ModelState.IsValid)
            return View(model);

        var email = NormalizeEmail(model.Email);

        var emailExists = await _context.Tourists.AnyAsync(
            item => item.Id != touristId && item.Email == email,
            cancellationToken);

        if (emailExists)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Email này đã được tài khoản du khách khác sử dụng.");

            return View(model);
        }

        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);

        if (tourist == null)
            return NotFound();

        tourist.FullName = model.FullName.Trim();
        tourist.Email = email;

        await _context.SaveChangesAsync(cancellationToken);

        await SignInTouristAsync(tourist, isPersistent: true);

        TempData["DuKhachSuccessMessage"] = "Cập nhật hồ sơ thành công.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new DuKhachChangePasswordViewModel());
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        DuKhachChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var touristId = GetTouristId();

        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);

        if (tourist == null)
            return NotFound();

        if (!_passwordService.Verify(model.CurrentPassword, tourist.PasswordHash, out _))
        {
            ModelState.AddModelError(
                nameof(model.CurrentPassword),
                "Mật khẩu hiện tại không chính xác.");

            return View(model);
        }

        tourist.PasswordHash = _passwordService.Hash(model.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["DuKhachSuccessMessage"]= "Đổi mật khẩu thành công. App vẫn đăng nhập bằng mật khẩu mới này.";
        return RedirectToAction(nameof(Profile));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        string email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng nhập email.");
            return View();
        }

        var normalizedEmail = NormalizeEmail(email);

        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

       TempData["DuKhachSuccessMessage"] = "Nếu email hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi đến hộp thư của bạn.";

        if (tourist == null || string.IsNullOrWhiteSpace(tourist.Email))
            return View();

        var token = CreateResetToken();
        var tokenHash = HashResetToken(token);

        var oldTokens = await _context.PasswordResetTokens
            .Where(item => item.TouristId == tourist.Id && item.UsedAt == null)
            .ToListAsync(cancellationToken);

        if (oldTokens.Count > 0)
            _context.PasswordResetTokens.RemoveRange(oldTokens);

        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            TouristId = tourist.Id,
            Email = tourist.Email,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });

        await _context.SaveChangesAsync(cancellationToken);

        var resetUrl = Url.ActionLink(
            nameof(ResetPassword),
            "Account",
            new { area = "DuKhach", email = tourist.Email, token });

        if (!string.IsNullOrWhiteSpace(resetUrl) && _emailSender.IsConfigured)
        {
            var body = $"""
                <div style="font-family:Arial,sans-serif;line-height:1.6;color:#102033">
                    <h2>Đặt lại mật khẩu VERSA Travel</h2>

                    <p>Xin chào {System.Net.WebUtility.HtmlEncode(tourist.FullName ?? "du khách")},</p>

                    <p>Bấm nút bên dưới để tạo mật khẩu mới. Liên kết này hết hạn sau 30 phút.</p>

                    <p>
                        <a href="{System.Net.WebUtility.HtmlEncode(resetUrl)}"
                           style="display:inline-block;padding:12px 18px;border-radius:12px;background:#34d399;color:#03131d;font-weight:700;text-decoration:none">
                            Đặt lại mật khẩu
                        </a>
                    </p>

                    <p>Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email.</p>
                </div>
                """;

            await _emailSender.SendAsync(
                tourist.Email,
                "Đặt lại mật khẩu VERSA Travel",
                body,
                cancellationToken);
        }
        else if (_environment.IsDevelopment() && !string.IsNullOrWhiteSpace(resetUrl))
        {
           TempData["DuKhachDebugResetLink"] = resetUrl;
        }

        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(
        string? email,
        string? token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["DuKhachErrorMessage"] = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var normalizedEmail = NormalizeEmail(email);
        var tokenHash = HashResetToken(token);
        var now = DateTime.UtcNow;

        var resetToken = await _context.PasswordResetTokens
            .AsNoTracking()
            .Include(item => item.Tourist)
            .FirstOrDefaultAsync(item =>
                item.Email == normalizedEmail &&
                item.TokenHash == tokenHash &&
                item.UsedAt == null &&
                item.ExpiresAt > now,
                cancellationToken);

        if (resetToken == null)
        {
            TempData["DuKhachErrorMessage"] = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        ViewBag.Email = resetToken.Email;
        ViewBag.Token = token;

        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        string email,
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken)
    {
        ViewBag.Email = email;
        ViewBag.Token = token;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            ModelState.AddModelError(string.Empty, "Liên kết đặt lại mật khẩu không hợp lệ.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            ModelState.AddModelError(string.Empty, "Mật khẩu mới phải có ít nhất 8 ký tự.");

        if (newPassword != confirmPassword)
            ModelState.AddModelError(string.Empty, "Mật khẩu xác nhận không khớp.");

        if (!ModelState.IsValid)
            return View();

        var normalizedEmail = NormalizeEmail(email);
        var tokenHash = HashResetToken(token);
        var now = DateTime.UtcNow;

        var resetToken = await _context.PasswordResetTokens
            .Include(item => item.Tourist)
            .FirstOrDefaultAsync(item =>
                item.Email == normalizedEmail &&
                item.TokenHash == tokenHash &&
                item.UsedAt == null &&
                item.ExpiresAt > now,
                cancellationToken);

        if (resetToken?.Tourist == null)
        {
            ModelState.AddModelError(string.Empty, "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
            return View();
        }

        resetToken.Tourist.PasswordHash = _passwordService.Hash(newPassword);
        resetToken.UsedAt = DateTime.UtcNow;

        var remainingTokens = await _context.PasswordResetTokens
            .Where(item =>
                item.TouristId == resetToken.TouristId &&
                item.Id != resetToken.Id &&
                item.UsedAt == null)
            .ToListAsync(cancellationToken);

        if (remainingTokens.Count > 0)
            _context.PasswordResetTokens.RemoveRange(remainingTokens);

        await _context.SaveChangesAsync(cancellationToken);

       TempData["DuKhachSuccessMessage"] = "Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(TouristScheme);
        return RedirectToAction("Index", "Home", new { area = "DuKhach" });
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpGet]
    public async Task<IActionResult> LogoutByGet()
    {
        await HttpContext.SignOutAsync(TouristScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInTouristAsync(Tourist tourist, bool isPersistent)
    {
        var displayName = string.IsNullOrWhiteSpace(tourist.FullName)
            ? tourist.Email ?? $"Du khách #{tourist.Id}"
            : tourist.FullName.Trim();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, tourist.Id.ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, tourist.Email ?? ""),
            new(ClaimTypes.Role, "Tourist"),
            new("AccountType", "Tourist")
        };

        var identity = new ClaimsIdentity(claims, TouristScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            TouristScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(isPersistent ? 30 : 1)
            });
    }

    private bool IsSignedInTourist()
    {
        return User.Identity?.IsAuthenticated == true &&
               User.IsInRole("Tourist") &&
               string.Equals(
                   User.FindFirstValue("AccountType"),
                   "Tourist",
                   StringComparison.OrdinalIgnoreCase);
    }

    private int GetTouristId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out var touristId))
            throw new InvalidOperationException("Không xác định được tài khoản du khách.");

        return touristId;
    }

    private IActionResult RedirectToSafeReturnUrl(string? returnUrl)
    {
        return Redirect(GetSafeDuKhachReturnUrl(returnUrl));
    }

    private string GetSafeDuKhachReturnUrl(string? returnUrl)
    {
        var fallbackUrl = Url.Action("Index", "Home", new { area = "DuKhach" }) ?? "/DuKhach/Home";

        if (string.IsNullOrWhiteSpace(returnUrl))
            return fallbackUrl;

        var url = returnUrl.Trim();

        if (!Url.IsLocalUrl(url))
            return fallbackUrl;

        if (url == "/" || url == "/#")
            return fallbackUrl;

        if (!url.StartsWith("/DuKhach", StringComparison.OrdinalIgnoreCase))
            return fallbackUrl;

        if (url.StartsWith("/DuKhach/Account/Login", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/DuKhach/Account/Register", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/DuKhach/Account/ForgotPassword", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/DuKhach/Account/ResetPassword", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/DuKhach/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/DuKhach/Account/LogoutByGet", StringComparison.OrdinalIgnoreCase))
        {
            return fallbackUrl;
        }

        return url;
    }

    private static string CreateResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string HashResetToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }
}