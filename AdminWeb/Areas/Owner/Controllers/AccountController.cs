using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
public sealed class AccountController : Controller
{
    private const string OwnerScheme = "OwnerScheme";

    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;

    public AccountController(AppDbContext context, PasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["Title"] = "Đăng nhập chủ gian hàng";
        ViewBag.ReturnUrl = GetSafeOwnerReturnUrl(returnUrl);

        if (User?.Identity?.IsAuthenticated == true && User.IsInRole("Owner"))
            return Redirect("/Areas/Owner");

        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string username,
        string password,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Đăng nhập chủ gian hàng";
        ViewBag.Username = username;
        ViewBag.ReturnUrl = GetSafeOwnerReturnUrl(returnUrl);

        username = (username ?? string.Empty).Trim();
        password ??= string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng nhập tài khoản và mật khẩu.");
            return View();
        }

        var user = await _context.Users
            .Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Username == username || item.Email == username, cancellationToken);

        if (user == null || !_passwordService.Verify(password, user.PasswordHash, out var needsUpgrade))
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
            return View();
        }

        if (!string.Equals(user.Role?.RoleName, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Trang này chỉ dành cho chủ gian hàng. Admin đăng nhập ở trang quản trị riêng.");
            return View();
        }

        if (!string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, string.Equals(user.Status, "pending", StringComparison.OrdinalIgnoreCase)
                ? "Tài khoản chủ gian hàng đang chờ admin duyệt."
                : "Tài khoản chủ gian hàng đang bị khóa hoặc chưa được kích hoạt.");
            return View();
        }

        var owner = await _context.OwnerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);

        if (owner == null)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản chưa có hồ sơ chủ gian hàng. Vui lòng liên hệ admin hoặc đăng ký lại hồ sơ.");
            return View();
        }

        if (!string.Equals(owner.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, string.Equals(owner.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                ? "Hồ sơ chủ gian hàng đang chờ admin duyệt."
                : "Hồ sơ chủ gian hàng chưa được kích hoạt.");
            return View();
        }

        if (needsUpgrade)
        {
            user.PasswordHash = _passwordService.Hash(password);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, "Owner"),
            new("OwnerProfileId", owner.Id.ToString()),
            new("BusinessName", owner.BusinessName ?? string.Empty)
        };

        var identity = new ClaimsIdentity(claims, OwnerScheme);
        var principal = new ClaimsPrincipal(identity);

        // Chỉ thay phiên Owner. Không xóa Admin/Editor/Reviewer/DuKhach để có thể mở nhiều portal cùng lúc.
        await HttpContext.SignOutAsync(OwnerScheme);
        await HttpContext.SignInAsync(OwnerScheme, principal);

        var safeReturnUrl = GetSafeOwnerReturnUrl(returnUrl);
        if (!string.IsNullOrWhiteSpace(safeReturnUrl))
            return LocalRedirect(safeReturnUrl);

        return Redirect("/Areas/Owner");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        ViewData["Title"] = "Đăng ký chủ gian hàng";
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        string username,
        string email,
        string password,
        string confirmPassword,
        string businessName,
        string? representativeName,
        string? phone,
        string? address,
        CancellationToken cancellationToken)
    {
        username = (username ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim().ToLowerInvariant();
        businessName = (businessName ?? string.Empty).Trim();
        representativeName = representativeName?.Trim();
        phone = phone?.Trim();
        address = address?.Trim();

        ViewBag.Username = username;
        ViewBag.Email = email;
        ViewBag.BusinessName = businessName;
        ViewBag.RepresentativeName = representativeName;
        ViewBag.Phone = phone;
        ViewBag.Address = address;

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(confirmPassword) ||
            string.IsNullOrWhiteSpace(businessName))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng nhập đủ username, email, mật khẩu và tên gian hàng.");
            return View();
        }

        if (username.Length < 4)
        {
            ModelState.AddModelError(string.Empty, "Username phải có ít nhất 4 ký tự.");
            return View();
        }

        if (!IsGmailAddress(email))
        {
            ModelState.AddModelError(string.Empty, "Chỉ chấp nhận đăng ký bằng Gmail có đuôi @gmail.com.");
            return View();
        }

        if (password.Length < 6)
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu phải có ít nhất 6 ký tự.");
            return View();
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu xác nhận không khớp.");
            return View();
        }

        var normalizedUsername = username.ToLowerInvariant();
        var exists = await _context.Users.AnyAsync(user =>
            user.Username.ToLower() == normalizedUsername ||
            user.Email.ToLower() == email,
            cancellationToken);

        if (exists)
        {
            ModelState.AddModelError(string.Empty, "Username hoặc email đã tồn tại.");
            return View();
        }

        var ownerRole = await _context.Roles.FirstOrDefaultAsync(role => role.RoleName == "Owner", cancellationToken);
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

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordService.Hash(password),
            RoleId = ownerRole.Id,
            Status = "pending",
            CreatedAt = DateTime.Now
        };

        var owner = new OwnerProfile
        {
            User = user,
            BusinessName = businessName,
            RepresentativeName = representativeName,
            Phone = phone,
            Address = address,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.OwnerProfiles.Add(owner);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["OwnerSuccessMessage"] = "Đã gửi đăng ký chủ gian hàng. Vui lòng chờ admin duyệt trước khi đăng nhập.";
        return RedirectToAction(nameof(Success), "Account", new { area = "Owner" });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Success()
    {
        ViewData["Title"] = "Đăng ký thành công";
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Không có quyền truy cập";
        return View();
    }

    [Authorize(AuthenticationSchemes = OwnerScheme)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(OwnerScheme);
        return Redirect("/Areas/Owner/Login");
    }

    private string? GetSafeOwnerReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return null;

        if (!Url.IsLocalUrl(returnUrl))
            return null;

        if (!returnUrl.StartsWith("/Owner", StringComparison.OrdinalIgnoreCase))
            return null;

        if (returnUrl.StartsWith("/Owner/Account/Login", StringComparison.OrdinalIgnoreCase) ||
            returnUrl.StartsWith("/Owner/Account/Register", StringComparison.OrdinalIgnoreCase) ||
            returnUrl.StartsWith("/Owner/Account/Logout", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return returnUrl;
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

}

