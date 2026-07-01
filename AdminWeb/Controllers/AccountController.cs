using AdminWeb.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using AdminWeb.Services;

namespace AdminWeb.Controllers
{
    [Authorize]
    public class AccountController : StaffPortalControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordService _passwordService;

        public AccountController(AppDbContext context, PasswordService passwordService)
            : base(context, passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        // GET: /Account/Login hoặc /Admin/Login
        // Cổng này chỉ dành riêng cho Admin.
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            ConfigureLoginView("Admin");

            var redirect = await PrepareLoginPortalAsync("Admin");
            if (redirect != null)
                return redirect;

            return View("Login");
        }

        // POST: /Account/Login hoặc /Admin/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [ActionName("Login")]
        public Task<IActionResult> LoginPost(string username, string password)
        {
            return LoginStaffAsync(username, password, "Admin", "Login");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult RedirectAdminOwnerToOwner(string? path = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Redirect("/Areas/Owner");

            return Redirect("/Areas/Owner/" + path.TrimStart('/'));
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> RedirectEditorApprovalToCorrectPortal(string? path = null)
        {
            var safePath = string.IsNullOrWhiteSpace(path) ? "PoiPending" : path.TrimStart('/');

            var reviewerAuth = await HttpContext.AuthenticateAsync("ReviewerScheme");
            if (reviewerAuth.Succeeded && reviewerAuth.Principal?.IsInRole("Reviewer") == true)
                return Redirect("/Areas/Reviewer/Approval/" + safePath);

            var adminAuth = await HttpContext.AuthenticateAsync("AdminScheme");
            if (adminAuth.Succeeded && adminAuth.Principal?.IsInRole("Admin") == true)
                return Redirect("/Admin/Approval/" + safePath);

            // Admin duyệt POI ở cổng Admin, không dùng prefix /Editor.
            return Redirect("/Admin/Approval/" + safePath);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register(string? area = null, string? returnUrl = null)
        {
            // AccountController gốc không dùng để đăng ký. Redirect về đúng khu vực
            // để URL cũ /Admin/Account/Register?area=Owner không còn 404.
            var redirect = RedirectWrongArea(area, returnUrl);
            if (redirect != null)
                return redirect;

            return RedirectToAction("Login", "Account", new { area = "" });
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

        [HttpPost("/Account/Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var roleName = User.Claims.FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
            return await SignOutStaffAndRedirectAsync(roleName);
        }

        // Logout của Admin; Editor và Reviewer có action riêng trong Area tương ứng.
        [AllowAnonymous]
        [HttpPost("/Admin/Logout")]
        [HttpPost("/Admin/Account/Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminLogout()
        {
            await HttpContext.SignOutAsync("AdminScheme");
            return Redirect("/Admin/Login");
        }

        // GET logout chỉ dùng làm lối thoát an toàn khi người dùng bấm nhầm link cũ hoặc cache cũ.
        [AllowAnonymous]
        [HttpGet("/Admin/Logout")]
        [HttpGet("/Admin/Account/Logout")]
        public async Task<IActionResult> AdminLogoutByGet()
        {
            await HttpContext.SignOutAsync("AdminScheme");
            return Redirect("/Admin/Login");
        }

        private async Task<IActionResult> SignOutStaffAndRedirectAsync(string? roleName)
        {
            var scheme = GetStaffScheme(roleName);
            await HttpContext.SignOutAsync(scheme);

            return roleName?.ToUpperInvariant() switch
            {
                "EDITOR" => Redirect("/Areas/Editor/Login"),
                "REVIEWER" => Redirect("/Areas/Reviewer/Login"),
                _ => Redirect("/Admin/Login")
            };
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

        private IActionResult? RedirectWrongArea(string? area, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(area))
                return null;

            if (string.Equals(area, "Owner", StringComparison.OrdinalIgnoreCase))
                return Redirect("/Areas/Owner/Login" + (string.IsNullOrWhiteSpace(returnUrl) ? "" : "?returnUrl=" + Uri.EscapeDataString(returnUrl)));

            if (string.Equals(area, "DuKhach", StringComparison.OrdinalIgnoreCase))
                return Redirect("/Areas/DuKhach/Account/Login" + (string.IsNullOrWhiteSpace(returnUrl) ? "" : "?returnUrl=" + Uri.EscapeDataString(returnUrl)));

            if (string.Equals(area, "Editor", StringComparison.OrdinalIgnoreCase))
                return Redirect("/Areas/Editor/Login");

            if (string.Equals(area, "Reviewer", StringComparison.OrdinalIgnoreCase))
                return Redirect("/Areas/Reviewer/Login");

            return null;
        }

    }
}
