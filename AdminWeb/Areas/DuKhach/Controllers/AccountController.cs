using AdminWeb.Areas.DuKhach.Models;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
public sealed class AccountController : Controller
{
    private const string TouristScheme = "TouristScheme";

    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly VisitorAchievementService _achievementService;
    private readonly PasswordResetService _passwordResetService;

    public AccountController(
        AppDbContext context,
        PasswordService passwordService,
        VisitorAchievementService achievementService,
        PasswordResetService passwordResetService)
    {
        _context = context;
        _passwordService = passwordService;
        _achievementService = achievementService;
        _passwordResetService = passwordResetService;
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

        if (!IsGmailAddress(email))
        {
            ModelState.AddModelError(nameof(model.Email), "Chỉ chấp nhận địa chỉ Gmail có đuôi @gmail.com.");
            return View(model);
        }

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
            return RedirectToAction(nameof(LogoutByGet), "Account", new { area = "DuKhach" });

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

        if (!IsGmailAddress(email))
        {
            ModelState.AddModelError(nameof(model.Email), "Chỉ chấp nhận địa chỉ Gmail có đuôi @gmail.com.");
            return View(model);
        }

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
        return RedirectToAction(nameof(Profile), "Account", new { area = "DuKhach" });
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
        return RedirectToAction(nameof(Profile), "Account", new { area = "DuKhach" });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword(string? email = null)
    {
        return View(new DuKhachForgotPasswordViewModel
        {
            Email = email?.Trim() ?? ""
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("PasswordResetPerIp")]
    public async Task<IActionResult> ForgotPassword(
        DuKhachForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = PasswordResetService.NormalizeEmail(model.Email);
        var result = await _passwordResetService.RequestOtpAsync(normalizedEmail, cancellationToken);

        TempData["DuKhachSuccessMessage"] =
            "Nếu email đã đăng ký, mã OTP 6 số đã được gửi. Mã có hiệu lực trong 10 phút.";

        if (!string.IsNullOrWhiteSpace(result.DebugOtp))
            TempData["DuKhachDebugOtp"] = result.DebugOtp;

        return RedirectToAction(
            nameof(ResetPassword),
            "Account",
            new { area = "DuKhach", email = normalizedEmail });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(ForgotPassword), "Account", new { area = "DuKhach" });
        }

        return View(new DuKhachResetPasswordViewModel
        {
            Email = PasswordResetService.NormalizeEmail(email)
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("PasswordResetPerIp")]
    public async Task<IActionResult> ResetPassword(
        DuKhachResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var status = await _passwordResetService.ResetPasswordAsync(
            model.Email,
            model.Otp,
            model.NewPassword,
            cancellationToken);

        if (status != PasswordResetStatus.Success)
        {
            ModelState.AddModelError(
                nameof(model.Otp),
                "Mã OTP không đúng, đã hết hạn hoặc đã được sử dụng.");
            return View(model);
        }

        TempData["DuKhachSuccessMessage"] =
            "Đặt lại mật khẩu thành công. Bạn có thể dùng mật khẩu mới trên web và app.";
        return Redirect("/Areas/DuKhach/Account/Login");
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(TouristScheme);
        return Redirect("/Areas/DuKhach");
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpGet]
    public async Task<IActionResult> LogoutByGet()
    {
        await HttpContext.SignOutAsync(TouristScheme);
        return Redirect("/Areas/DuKhach/Account/Login");
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

        // Chỉ thay phiên DuKhach. Không xóa Owner/Admin/Editor/Reviewer để có thể mở nhiều portal cùng lúc.
        await HttpContext.SignOutAsync(TouristScheme);

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
        var fallbackUrl = Url.Action("Index", "Home", new { area = "DuKhach" }) ?? "/Areas/DuKhach";

        if (string.IsNullOrWhiteSpace(returnUrl))
            return fallbackUrl;

        var url = returnUrl.Trim();

        if (!Url.IsLocalUrl(url))
            return fallbackUrl;

        if (url == "/" || url == "/#")
            return fallbackUrl;

        if (!url.StartsWith("/Areas/DuKhach", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("/DuKhach", StringComparison.OrdinalIgnoreCase))
            return fallbackUrl;

        if (url.StartsWith("/Areas/DuKhach/Account/Login", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/Areas/DuKhach/Account/Register", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/Areas/DuKhach/Account/ForgotPassword", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/Areas/DuKhach/Account/ResetPassword", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/Areas/DuKhach/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/Areas/DuKhach/Account/LogoutByGet", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/DuKhach/Account/Login", StringComparison.OrdinalIgnoreCase) ||
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

    private static bool IsGmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var atIndex = email.IndexOf('@');

        return atIndex > 0 &&
               atIndex == email.LastIndexOf('@') &&
               email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }
}
