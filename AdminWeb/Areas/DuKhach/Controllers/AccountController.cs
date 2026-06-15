using AdminWeb.Areas.DuKhach.Models;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
public sealed class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly VisitorAchievementService _achievementService;

    public AccountController(
        AppDbContext context,
        PasswordService passwordService,
        VisitorAchievementService achievementService)
    {
        _context = context;
        _passwordService = passwordService;
        _achievementService = achievementService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (IsSignedInTourist())
            return RedirectToSafeReturnUrl(returnUrl);

        return View(new DuKhachRegisterViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(DuKhachRegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = NormalizeEmail(model.Email);
        if (await _context.Tourists.AnyAsync(item => item.Email == email, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã được đăng ký. Bạn có thể đăng nhập bằng email này trên cả web và app.");
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
        TempData["SuccessMessage"] = "Đăng ký thành công. Tài khoản này có thể đăng nhập trên app du lịch.";
        return RedirectToSafeReturnUrl(model.ReturnUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (IsSignedInTourist())
            return RedirectToSafeReturnUrl(returnUrl);

        return View(new DuKhachLoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(DuKhachLoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = NormalizeEmail(model.Email);
        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Email == email, cancellationToken);

        if (tourist == null || !_passwordService.Verify(model.Password, tourist.PasswordHash, out var needsUpgrade))
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
        TempData["SuccessMessage"] = "Đăng nhập du khách thành công.";
        return RedirectToSafeReturnUrl(model.ReturnUrl);
    }

    [Authorize(Roles = "Tourist")]
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

    [Authorize(Roles = "Tourist")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(DuKhachProfileViewModel model, CancellationToken cancellationToken)
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
            ModelState.AddModelError(nameof(model.Email), "Email này đã được tài khoản du khách khác sử dụng.");
            return View(model);
        }

        var tourist = await _context.Tourists.FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);
        if (tourist == null)
            return NotFound();

        tourist.FullName = model.FullName.Trim();
        tourist.Email = email;
        await _context.SaveChangesAsync(cancellationToken);

        await SignInTouristAsync(tourist, isPersistent: true);
        TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize(Roles = "Tourist")]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new DuKhachChangePasswordViewModel());
    }

    [Authorize(Roles = "Tourist")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(DuKhachChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var touristId = GetTouristId();
        var tourist = await _context.Tourists.FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);
        if (tourist == null)
            return NotFound();

        if (!_passwordService.Verify(model.CurrentPassword, tourist.PasswordHash, out _))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không chính xác.");
            return View(model);
        }

        tourist.PasswordHash = _passwordService.Hash(model.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đổi mật khẩu thành công. App vẫn đăng nhập bằng mật khẩu mới này.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize(Roles = "Tourist")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home", new { area = "DuKhach" });
    }

    [Authorize(Roles = "Tourist")]
    [HttpGet]
    public async Task<IActionResult> LogoutByGet()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(isPersistent ? 30 : 1)
            });
    }

    private bool IsSignedInTourist() =>
        User.Identity?.IsAuthenticated == true && User.IsInRole("Tourist");

    private int GetTouristId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private IActionResult RedirectToSafeReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home", new { area = "DuKhach" });
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();
}
