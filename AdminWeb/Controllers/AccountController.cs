using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using AdminWeb.Services;

namespace AdminWeb.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordService _passwordService;

        public AccountController(AppDbContext context, PasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        // GET: /Account/Login
        [AllowAnonymous]
        public IActionResult Login()
        {
            // Nếu đã đăng nhập rồi thì chuyển thẳng vào trang chủ (Dashboard)
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToWorkspace(User.Claims
                    .FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.Role)?.Value);
            }
            return View();
        }

        // POST: /Account/Login
[HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Cho phép tìm kiếm người dùng bằng Username HOẶC Email
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username || u.Email == username);
            
            if (user != null && user.Status == "active")
            {
                if (_passwordService.Verify(password, user.PasswordHash, out var needsUpgrade))
                {
                    if (needsUpgrade)
                    {
                        user.PasswordHash = _passwordService.Hash(password);
                        await _context.SaveChangesAsync();
                    }

                    var claims = new List<System.Security.Claims.Claim>
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Username),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role?.RoleName ?? "User")
                    };

                    var identity = new System.Security.Claims.ClaimsIdentity(claims, "AdminScheme");
                    var principal = new System.Security.Claims.ClaimsPrincipal(identity);

                    // Ghi Cookie phiên đăng nhập
                    await HttpContext.SignInAsync("AdminScheme", principal);
                    return RedirectToWorkspace(user.Role?.RoleName);
                }
            }

            // Báo lỗi nếu sai tài khoản / mật khẩu
            ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không chính xác.");
            return View();
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: /Account/Profile
        public async Task<IActionResult> Profile()
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();
            
            return View(user);
        }

        // POST: /Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string Email)
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            
            if (user != null)
            {
                user.Email = Email;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
            }
            return RedirectToAction(nameof(Profile));
        }

        // GET: /Account/ChangePassword
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "Mật khẩu xác nhận không khớp!";
                return View();
            }
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                TempData["ErrorMessage"] = "Mật khẩu mới phải có ít nhất 8 ký tự.";
                return View();
            }

            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            
            if (user != null)
            {
                if (!_passwordService.Verify(oldPassword, user.PasswordHash, out _))
                {
                    TempData["ErrorMessage"] = "Mật khẩu hiện tại không chính xác!";
                    return View();
                }

                user.PasswordHash = _passwordService.Hash(newPassword);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return RedirectToAction(nameof(Profile));
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminScheme");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        private IActionResult RedirectToWorkspace(string? roleName)
        {
            return roleName?.ToUpperInvariant() switch
            {
                "EDITOR" => RedirectToAction("Index", "Dashboard", new { area = "Editor" }),
                "REVIEWER" => RedirectToAction("Index", "Dashboard", new { area = "Reviewer" }),
                "TOURIST" => RedirectToAction("Index", "Home", new { area = "DuKhach" }),
                _ => RedirectToAction("Index", "Home", new { area = "" })
            };
        }
    }
}
