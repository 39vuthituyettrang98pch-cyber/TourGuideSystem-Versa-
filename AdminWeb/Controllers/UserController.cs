using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdminWeb.Services;

namespace AdminWeb.Controllers;

[Authorize(Policy = "AdminAreaPolicy")]
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
            .OrderBy(u => u.Role!.RoleName)
            .ThenBy(u => u.Username)
            .ToListAsync();

        ViewBag.StaffCount = users.Count;
        ViewBag.TouristCount = await _context.Tourists.CountAsync();

        return View(users);
    }

    public async Task<IActionResult> Create()
    {
        await LoadRolesAsync();
        return View(new User());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        User user,
        string? ownerBusinessName,
        string? ownerRepresentativeName,
        string? ownerPhone,
        string? ownerAddress)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordHash) || user.PasswordHash.Length < 8)
            ModelState.AddModelError(nameof(user.PasswordHash), "Mật khẩu phải có ít nhất 8 ký tự.");

        await ValidateUserAsync(user);

        if (ModelState.IsValid)
        {
            user.Username = user.Username.Trim();
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.Status = NormalizeStaffStatus(user.Status);
            user.CreatedAt = DateTime.Now;
            user.PasswordHash = _passwordService.Hash(user.PasswordHash);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await EnsureOwnerProfileAsync(
                user,
                ownerBusinessName,
                ownerRepresentativeName,
                ownerPhone,
                ownerAddress);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo tài khoản {user.Username}.";
            return Redirect("/Admin/User");
        }

        await LoadRolesAsync();
        return View(user);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (user == null)
            return NotFound();

        await LoadRolesAsync();
        ViewBag.OwnerProfile = await _context.OwnerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(owner => owner.UserId == user.Id);

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        User user,
        string? newPassword,
        string? ownerBusinessName,
        string? ownerRepresentativeName,
        string? ownerPhone,
        string? ownerAddress)
    {
        if (id != user.Id)
            return NotFound();

        user.Status = NormalizeStaffStatus(user.Status);

        await ValidateUserAsync(user, id);

        if (!string.IsNullOrWhiteSpace(newPassword) && newPassword.Length < 8)
            ModelState.AddModelError(nameof(newPassword), "Mật khẩu mới phải có ít nhất 8 ký tự.");

        if (ModelState.IsValid)
        {
            var existingUser = await _context.Users
                .Include(item => item.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (existingUser == null)
                return NotFound();

            if (string.Equals(existingUser.Username, User.Identity?.Name, StringComparison.OrdinalIgnoreCase) &&
                (user.RoleId != existingUser.RoleId ||
                 !string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("", "Bạn không thể đổi quyền hoặc khóa tài khoản đang đăng nhập.");
                await LoadRolesAsync();
                ViewBag.OwnerProfile = await _context.OwnerProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(owner => owner.UserId == user.Id);
                return View(user);
            }

            existingUser.Email = user.Email.Trim().ToLowerInvariant();
            existingUser.RoleId = user.RoleId;
            existingUser.Status = user.Status;

            if (!string.IsNullOrWhiteSpace(newPassword))
                existingUser.PasswordHash = _passwordService.Hash(newPassword);

            await EnsureOwnerProfileAsync(
                existingUser,
                ownerBusinessName,
                ownerRepresentativeName,
                ownerPhone,
                ownerAddress);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật tài khoản {existingUser.Username}.";
            return Redirect("/Admin/User");
        }

        await LoadRolesAsync();
        ViewBag.OwnerProfile = await _context.OwnerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(owner => owner.UserId == user.Id);

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.Users
            .Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (user != null)
        {
            if (string.Equals(user.Username, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Bạn không thể xóa tài khoản đang đăng nhập.";
                return Redirect("/Admin/User");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa tài khoản hệ thống.";
        }

        return Redirect("/Admin/User");
    }

    [HttpGet]
    public async Task<IActionResult> Tourists(string? q = null)
    {
        var query = _context.Tourists.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim().ToLowerInvariant();
            query = query.Where(tourist =>
                (tourist.Email ?? "").ToLower().Contains(keyword) ||
                (tourist.FullName ?? "").ToLower().Contains(keyword));
        }

        var tourists = await query
            .OrderByDescending(tourist => tourist.CreatedAt)
            .ToListAsync();

        var touristIds = tourists.Select(tourist => tourist.Id).ToList();

        ViewBag.Query = q?.Trim() ?? "";
        ViewBag.StaffCount = await _context.Users.CountAsync();
        ViewBag.TouristCount = await _context.Tourists.CountAsync();

        ViewBag.OrderCounts = await _context.MenuOrders
            .AsNoTracking()
            .Where(order => touristIds.Contains(order.TouristId))
            .GroupBy(order => order.TouristId)
            .Select(group => new { TouristId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TouristId, item => item.Count);

        ViewBag.DiscoveryCounts = await _context.TouristPoiDiscoveries
            .AsNoTracking()
            .Where(item => touristIds.Contains(item.TouristId))
            .GroupBy(item => item.TouristId)
            .Select(group => new { TouristId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TouristId, item => item.Count);

        return View(tourists);
    }

    [HttpGet]
    public IActionResult CreateTourist()
    {
        return View(new Tourist
        {
            CreatedAt = DateTime.Now,
            AuthProvider = "local"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTourist(Tourist tourist, string password)
    {
        tourist.FullName = tourist.FullName?.Trim();
        tourist.Email = NormalizeEmail(tourist.Email);
        tourist.AuthProvider = "local";
        tourist.CreatedAt = DateTime.Now;

        ValidateTourist(tourist);

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            ModelState.AddModelError(nameof(password), "Mật khẩu phải có ít nhất 8 ký tự.");

        if (await _context.Tourists.AnyAsync(item => item.Email == tourist.Email))
            ModelState.AddModelError(nameof(tourist.Email), "Email du khách này đã tồn tại.");

        if (ModelState.IsValid)
        {
            tourist.PasswordHash = _passwordService.Hash(password);
            _context.Tourists.Add(tourist);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã tạo tài khoản du khách.";
            return Redirect("/Admin/User/Tourists");
        }

        return View(tourist);
    }

    [HttpGet]
    public async Task<IActionResult> EditTourist(int? id)
    {
        if (id == null)
            return NotFound();

        var tourist = await _context.Tourists
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id.Value);

        if (tourist == null)
            return NotFound();

        ViewBag.OrderCount = await _context.MenuOrders.CountAsync(order => order.TouristId == tourist.Id);
        ViewBag.DiscoveryCount = await _context.TouristPoiDiscoveries.CountAsync(item => item.TouristId == tourist.Id);
        ViewBag.SubscriptionCount = await _context.TouristSubscriptions.CountAsync(item => item.TouristId == tourist.Id);

        return View(tourist);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTourist(int id, Tourist model, string? newPassword)
    {
        if (id != model.Id)
            return NotFound();

        var tourist = await _context.Tourists.FirstOrDefaultAsync(item => item.Id == id);
        if (tourist == null)
            return NotFound();

        tourist.FullName = model.FullName?.Trim();
        tourist.Email = NormalizeEmail(model.Email);
        tourist.AuthProvider = string.IsNullOrWhiteSpace(tourist.AuthProvider) ? "local" : tourist.AuthProvider;

        ValidateTourist(tourist, existingTouristId: id);

        if (!string.IsNullOrWhiteSpace(newPassword) && newPassword.Length < 8)
            ModelState.AddModelError(nameof(newPassword), "Mật khẩu mới phải có ít nhất 8 ký tự.");

        if (ModelState.IsValid)
        {
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                tourist.PasswordHash = _passwordService.Hash(newPassword);
                tourist.AuthProvider = "local";
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật tài khoản du khách.";
            return Redirect("/Admin/User/Tourists");
        }

        ViewBag.OrderCount = await _context.MenuOrders.CountAsync(order => order.TouristId == tourist.Id);
        ViewBag.DiscoveryCount = await _context.TouristPoiDiscoveries.CountAsync(item => item.TouristId == tourist.Id);
        ViewBag.SubscriptionCount = await _context.TouristSubscriptions.CountAsync(item => item.TouristId == tourist.Id);

        return View(tourist);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTourist(int id)
    {
        var tourist = await _context.Tourists.FirstOrDefaultAsync(item => item.Id == id);

        if (tourist == null)
            return Redirect("/Admin/User/Tourists");

        var hasMenuOrders = await _context.MenuOrders.AnyAsync(order => order.TouristId == id);
        var hasPayments = await _context.PaymentTransactions.AnyAsync(payment => payment.TouristId == id);
        var hasSubscriptions = await _context.TouristSubscriptions.AnyAsync(subscription => subscription.TouristId == id);

        if (hasMenuOrders || hasPayments || hasSubscriptions)
        {
            TempData["ErrorMessage"] =
                "Không thể xóa du khách vì đã có đơn hàng, giao dịch hoặc gói premium. Bạn vẫn có thể sửa tên, email hoặc đổi mật khẩu.";
            return Redirect("/Admin/User/Tourists");
        }

        var bookmarks = await _context.TouristBookmarks.Where(item => item.TouristId == id).ToListAsync();
        var favorites = await _context.TouristFavorites.Where(item => item.TouristId == id).ToListAsync();
        var discoveries = await _context.TouristPoiDiscoveries.Where(item => item.TouristId == id).ToListAsync();
        var playbackLogs = await _context.VisitorPlaybackLogs.Where(item => item.TouristId == id).ToListAsync();
        var resetTokens = await _context.PasswordResetTokens.Where(item => item.TouristId == id).ToListAsync();
        var reviews = await _context.Reviews.Where(item => item.TouristId == id).ToListAsync();

        _context.TouristBookmarks.RemoveRange(bookmarks);
        _context.TouristFavorites.RemoveRange(favorites);
        _context.TouristPoiDiscoveries.RemoveRange(discoveries);
        _context.VisitorPlaybackLogs.RemoveRange(playbackLogs);
        _context.PasswordResetTokens.RemoveRange(resetTokens);

        foreach (var review in reviews)
            review.TouristId = null;

        _context.Tourists.Remove(tourist);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa tài khoản du khách.";
        return Redirect("/Admin/User/Tourists");
    }

    private async Task LoadRolesAsync()
    {
        ViewBag.Roles = await _context.Roles
            .AsNoTracking()
            .Where(role => role.RoleName == "Admin" ||
                           role.RoleName == "Editor" ||
                           role.RoleName == "Reviewer" ||
                           role.RoleName == "Owner")
            .OrderBy(role => role.RoleName)
            .ToListAsync();
    }

    private async Task EnsureOwnerProfileAsync(
        User user,
        string? businessName,
        string? representativeName,
        string? phone,
        string? address)
    {
        var selectedRole = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Id == user.RoleId);

        if (!string.Equals(selectedRole?.RoleName, "Owner", StringComparison.OrdinalIgnoreCase))
            return;

        businessName = businessName?.Trim();
        representativeName = representativeName?.Trim();
        phone = phone?.Trim();
        address = address?.Trim();

        var ownerProfile = await _context.OwnerProfiles
            .FirstOrDefaultAsync(owner => owner.UserId == user.Id);

        if (ownerProfile == null)
        {
            ownerProfile = new OwnerProfile
            {
                UserId = user.Id,
                BusinessName = string.IsNullOrWhiteSpace(businessName)
                    ? $"Gian hàng {user.Username}"
                    : businessName,
                RepresentativeName = string.IsNullOrWhiteSpace(representativeName) ? user.Username : representativeName,
                Phone = phone,
                Address = address,
                Status = string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase) ? "Active" : "Pending",
                CreatedAt = DateTime.UtcNow
            };
            _context.OwnerProfiles.Add(ownerProfile);
            return;
        }

        if (!string.IsNullOrWhiteSpace(businessName))
            ownerProfile.BusinessName = businessName;

        if (!string.IsNullOrWhiteSpace(representativeName))
            ownerProfile.RepresentativeName = representativeName;

        if (!string.IsNullOrWhiteSpace(phone))
            ownerProfile.Phone = phone;

        if (!string.IsNullOrWhiteSpace(address))
            ownerProfile.Address = address;

        ownerProfile.Status = string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase) ? "Active" : "Pending";
    }

    private async Task ValidateUserAsync(User user, int? existingUserId = null)
    {
        user.Username = user.Username?.Trim() ?? "";
        user.Email = user.Email?.Trim().ToLowerInvariant() ?? "";
        user.Status = NormalizeStaffStatus(user.Status);

        if (string.IsNullOrWhiteSpace(user.Username))
            ModelState.AddModelError(nameof(user.Username), "Tên đăng nhập là bắt buộc.");

        if (string.IsNullOrWhiteSpace(user.Email))
            ModelState.AddModelError(nameof(user.Email), "Email là bắt buộc.");

        if (!IsGmailAddress(user.Email))
            ModelState.AddModelError(nameof(user.Email), "Chỉ chấp nhận email Gmail có đuôi @gmail.com.");

        if (!await _context.Roles.AnyAsync(role =>
                role.Id == user.RoleId &&
                (role.RoleName == "Admin" ||
                 role.RoleName == "Editor" ||
                 role.RoleName == "Reviewer" ||
                 role.RoleName == "Owner")))
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

    private void ValidateTourist(Tourist tourist, int? existingTouristId = null)
    {
        tourist.FullName = tourist.FullName?.Trim();
        tourist.Email = NormalizeEmail(tourist.Email);

        if (string.IsNullOrWhiteSpace(tourist.FullName))
            ModelState.AddModelError(nameof(tourist.FullName), "Họ tên du khách là bắt buộc.");

        if (string.IsNullOrWhiteSpace(tourist.Email))
            ModelState.AddModelError(nameof(tourist.Email), "Email du khách là bắt buộc.");

        if (!IsGmailAddress(tourist.Email ?? ""))
            ModelState.AddModelError(nameof(tourist.Email), "Chỉ chấp nhận email Gmail có đuôi @gmail.com.");

        if (_context.Tourists.Any(existing =>
                existing.Id != existingTouristId &&
                existing.Email == tourist.Email))
        {
            ModelState.AddModelError(nameof(tourist.Email), "Email du khách này đã tồn tại.");
        }
    }

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeStaffStatus(string? status)
    {
        return string.Equals(status, "disabled", StringComparison.OrdinalIgnoreCase)
            ? "disabled"
            : "active";
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
