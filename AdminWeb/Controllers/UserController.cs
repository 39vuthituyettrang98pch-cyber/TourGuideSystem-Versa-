using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdminWeb.Services;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")] // Chỉ tài khoản có Role "Admin" mới được phép vào
public class UserController : Controller
{
    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;

    public UserController(AppDbContext context, PasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .OrderBy(u => u.Username)
            .ToListAsync();
        return View(users);
    }

    public async Task<IActionResult> Create()
    {
        await LoadRolesAsync();
        return View(new User());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(User user)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordHash) || user.PasswordHash.Length < 8)
            ModelState.AddModelError(nameof(user.PasswordHash), "Mật khẩu phải có ít nhất 8 ký tự.");

        await ValidateUserAsync(user);

        if (ModelState.IsValid)
        {
            user.Username = user.Username.Trim();
            user.Email = user.Email.Trim();
            user.CreatedAt = DateTime.Now;
            user.PasswordHash = _passwordService.Hash(user.PasswordHash);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã tạo tài khoản {user.Username}.";
            return RedirectToAction(nameof(Index));
        }
        await LoadRolesAsync();
        return View(user);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        
        await LoadRolesAsync();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, User user, string? newPassword)
    {
        if (id != user.Id) return NotFound();

        await ValidateUserAsync(user, id);
        if (!string.IsNullOrWhiteSpace(newPassword) && newPassword.Length < 8)
            ModelState.AddModelError(nameof(newPassword), "Mật khẩu mới phải có ít nhất 8 ký tự.");

        if (ModelState.IsValid)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (existingUser == null) return NotFound();

            if (string.Equals(existingUser.Username, User.Identity?.Name, StringComparison.OrdinalIgnoreCase) &&
                (user.RoleId != existingUser.RoleId || user.Status != "active"))
            {
                ModelState.AddModelError("", "Bạn không thể đổi quyền hoặc khóa tài khoản đang đăng nhập.");
                await LoadRolesAsync();
                return View(user);
            }

            existingUser.Email = user.Email.Trim();
            existingUser.RoleId = user.RoleId;
            existingUser.Status = user.Status;
            if (!string.IsNullOrWhiteSpace(newPassword))
                existingUser.PasswordHash = _passwordService.Hash(newPassword);

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật tài khoản {existingUser.Username}.";
            return RedirectToAction(nameof(Index));
        }
        await LoadRolesAsync();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            if (string.Equals(user.Username, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Bạn không thể xóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index));
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa tài khoản quản trị.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadRolesAsync()
    {
        ViewBag.Roles = await _context.Roles
            .AsNoTracking()
            .Where(role => role.RoleName == "Admin" || role.RoleName == "Editor" || role.RoleName == "Reviewer")
            .OrderBy(role => role.RoleName)
            .ToListAsync();
    }

    private async Task ValidateUserAsync(User user, int? existingUserId = null)
    {
        user.Username = user.Username?.Trim() ?? "";
        user.Email = user.Email?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(user.Username))
            ModelState.AddModelError(nameof(user.Username), "Tên đăng nhập là bắt buộc.");
        if (string.IsNullOrWhiteSpace(user.Email))
            ModelState.AddModelError(nameof(user.Email), "Email là bắt buộc.");

        if (!await _context.Roles.AnyAsync(role =>
                role.Id == user.RoleId &&
                (role.RoleName == "Admin" || role.RoleName == "Editor" || role.RoleName == "Reviewer")))
        {
            ModelState.AddModelError(nameof(user.RoleId), "Vai trò được chọn không hợp lệ.");
        }

        if (await _context.Users.AnyAsync(existing =>
                existing.Id != existingUserId && existing.Username == user.Username))
        {
            ModelState.AddModelError(nameof(user.Username), "Tên đăng nhập đã tồn tại.");
        }

        if (await _context.Users.AnyAsync(existing =>
                existing.Id != existingUserId && existing.Email == user.Email))
        {
            ModelState.AddModelError(nameof(user.Email), "Email đã tồn tại.");
        }
    }
}
