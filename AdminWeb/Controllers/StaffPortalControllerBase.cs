using AdminWeb.Data;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Controllers;

/// <summary>
/// Shares staff authentication behavior without exposing Admin actions inside
/// the Editor and Reviewer areas.
/// </summary>
public abstract class StaffPortalControllerBase : Controller
{
    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;

    protected StaffPortalControllerBase(AppDbContext context, PasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    protected Task<IActionResult?> PrepareLoginPortalAsync(string expectedRole)
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated)
            return Task.FromResult<IActionResult?>(null);

        var currentRole = User.Claims
            .FirstOrDefault(claim => claim.Type == ClaimTypes.Role)?.Value;

        if (string.Equals(currentRole, expectedRole, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<IActionResult?>(RedirectToWorkspace(currentRole));

        return Task.FromResult<IActionResult?>(null);
    }

    protected async Task<IActionResult> LoginStaffAsync(
        string username,
        string password,
        string expectedRole,
        string viewName)
    {
        ConfigureLoginView(expectedRole);
        username = (username ?? string.Empty).Trim();
        password ??= string.Empty;

        var user = await _context.Users
            .Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Username == username || item.Email == username);
        var roleName = user?.Role?.RoleName ?? string.Empty;

        if (user != null && !string.Equals(roleName, expectedRole, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("", $"Tài khoản này thuộc vai trò {GetRoleDisplayName(roleName)}. Vui lòng đăng nhập đúng trang dành cho vai trò đó.");
            ViewBag.CorrectLoginUrl = GetLoginUrl(roleName);
            ViewBag.CorrectLoginText = $"Đi tới trang đăng nhập {GetRoleDisplayName(roleName)}";
            return View(viewName);
        }

        if (user != null && !string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("", string.Equals(user.Status, "pending", StringComparison.OrdinalIgnoreCase)
                ? "Tài khoản đang chờ admin duyệt."
                : "Tài khoản đang bị khóa hoặc chưa được kích hoạt.");
            return View(viewName);
        }

        if (user != null && _passwordService.Verify(password, user.PasswordHash, out var needsUpgrade))
        {
            if (needsUpgrade)
            {
                user.PasswordHash = _passwordService.Hash(password);
                await _context.SaveChangesAsync();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, roleName)
            };

            var scheme = GetStaffScheme(expectedRole);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));

            await HttpContext.SignOutAsync(scheme);
            await HttpContext.SignInAsync(scheme, principal);
            return RedirectToWorkspace(roleName);
        }

        ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        return View(viewName);
    }

    protected void ConfigureLoginView(string expectedRole)
    {
        ViewBag.ExpectedRole = expectedRole;
        ViewBag.ExpectedRoleDisplayName = GetRoleDisplayName(expectedRole);
        ViewBag.AdminLoginUrl = "/Admin/Login";
        ViewBag.EditorLoginUrl = "/Areas/Editor/Login";
        ViewBag.ReviewerLoginUrl = "/Areas/Reviewer/Login";
        ViewBag.OwnerLoginUrl = "/Areas/Owner/Login";
        ViewBag.TouristLoginUrl = "/Areas/DuKhach/Account/Login";
    }

    private static string GetStaffScheme(string? roleName)
    {
        return roleName?.ToUpperInvariant() switch
        {
            "EDITOR" => "EditorScheme",
            "REVIEWER" => "ReviewerScheme",
            _ => "AdminScheme"
        };
    }

    private static string GetRoleDisplayName(string? roleName)
    {
        return roleName?.ToUpperInvariant() switch
        {
            "ADMIN" => "Admin",
            "EDITOR" => "Editor",
            "REVIEWER" => "Reviewer",
            "OWNER" => "Chủ gian hàng",
            "TOURIST" => "Du khách",
            _ => "tài khoản hệ thống"
        };
    }

    private static string? GetLoginUrl(string? roleName)
    {
        return roleName?.ToUpperInvariant() switch
        {
            "ADMIN" => "/Admin/Login",
            "EDITOR" => "/Areas/Editor/Login",
            "REVIEWER" => "/Areas/Reviewer/Login",
            "OWNER" => "/Areas/Owner/Login",
            "TOURIST" => "/Areas/DuKhach/Account/Login",
            _ => null
        };
    }

    private IActionResult RedirectToWorkspace(string? roleName)
    {
        return roleName?.ToUpperInvariant() switch
        {
            "ADMIN" => Redirect("/Admin"),
            "EDITOR" => Redirect("/Areas/Editor"),
            "REVIEWER" => Redirect("/Areas/Reviewer"),
            "OWNER" => Redirect("/Areas/Owner"),
            "TOURIST" => Redirect("/Areas/DuKhach"),
            _ => Redirect("/Admin")
        };
    }
}
