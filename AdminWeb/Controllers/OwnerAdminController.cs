using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class OwnerAdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;

    public OwnerAdminController(AppDbContext context, PasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owners = await _context.OwnerProfiles
            .Include(owner => owner.User)
            .Include(owner => owner.Pois)
            .Include(owner => owner.Subscriptions)
                .ThenInclude(subscription => subscription.PaymentPlan)
            .OrderByDescending(owner => owner.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(owners);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewBag.OwnerRoleId = await EnsureOwnerRoleAsync(cancellationToken);
        return View(new OwnerProfile { Status = "Active" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string username,
        string email,
        string password,
        string businessName,
        string? representativeName,
        string? phone,
        string? address,
        string status,
        CancellationToken cancellationToken)
    {
        username = (username ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim().ToLowerInvariant();
        businessName = (businessName ?? string.Empty).Trim();
        status = string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(businessName))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng nhập đủ tên đăng nhập, email, mật khẩu và tên gian hàng.");
            return View(new OwnerProfile { BusinessName = businessName, RepresentativeName = representativeName, Phone = phone, Address = address, Status = status });
        }

        if (password.Length < 6)
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu chủ gian hàng phải có ít nhất 6 ký tự.");
            return View(new OwnerProfile { BusinessName = businessName, RepresentativeName = representativeName, Phone = phone, Address = address, Status = status });
        }

        var exists = await _context.Users.AnyAsync(user => user.Username == username || user.Email == email, cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(string.Empty, "Username hoặc email đã tồn tại.");
            return View(new OwnerProfile { BusinessName = businessName, RepresentativeName = representativeName, Phone = phone, Address = address, Status = status });
        }

        var ownerRoleId = await EnsureOwnerRoleAsync(cancellationToken);
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordService.Hash(password),
            RoleId = ownerRoleId,
            Status = status switch
            {
                "Active" => "active",
                "Pending" => "pending",
                "Locked" => "locked",
                _ => "active"
            },
            CreatedAt = DateTime.Now
        };

        var owner = new OwnerProfile
        {
            User = user,
            BusinessName = businessName,
            RepresentativeName = representativeName,
            Phone = phone,
            Address = address,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        _context.OwnerProfiles.Add(owner);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã tạo tài khoản chủ gian hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var owner = await _context.OwnerProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (owner == null)
            return NotFound();

        return View(owner);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string businessName, string? representativeName, string? phone, string? address, string status, CancellationToken cancellationToken)
    {
        var owner = await _context.OwnerProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (owner == null)
            return NotFound();

        owner.BusinessName = (businessName ?? string.Empty).Trim();
        owner.RepresentativeName = representativeName;
        owner.Phone = phone;
        owner.Address = address;
        owner.Status = string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();

        if (owner.User != null)
        {
            owner.User.Status = owner.Status switch
            {
                "Active" => "active",
                "Pending" => "pending",
                "Locked" => "locked",
                _ => owner.User.Status
            };
        }

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã cập nhật chủ gian hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AssignPoi(int id, CancellationToken cancellationToken)
    {
        var owner = await _context.OwnerProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (owner == null)
            return NotFound();

        ViewBag.Pois = await _context.Pois
            .Include(poi => poi.Translations)
            .OrderBy(poi => poi.Id)
            .ToListAsync(cancellationToken);

        return View(owner);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPoi(int id, int poiId, CancellationToken cancellationToken)
    {
        var owner = await _context.OwnerProfiles.FindAsync([id], cancellationToken);
        var poi = await _context.Pois.FindAsync([poiId], cancellationToken);

        if (owner == null || poi == null)
            return NotFound();

        poi.OwnerProfileId = owner.Id;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã gán POI cho chủ gian hàng.";
        return RedirectToAction(nameof(AssignPoi), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePoi(int id, int poiId, CancellationToken cancellationToken)
    {
        var poi = await _context.Pois.FirstOrDefaultAsync(item => item.Id == poiId && item.OwnerProfileId == id, cancellationToken);
        if (poi == null)
            return NotFound();

        poi.OwnerProfileId = null;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã bỏ gán POI khỏi chủ gian hàng.";
        return RedirectToAction(nameof(AssignPoi), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var owner = await _context.OwnerProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (owner == null)
            return NotFound();

        owner.Status = "Active";
        if (owner.User != null)
            owner.User.Status = "active";

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã duyệt tài khoản chủ gian hàng. Chủ gian hàng có thể đăng nhập.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(int id, CancellationToken cancellationToken)
    {
        var owner = await _context.OwnerProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (owner == null)
            return NotFound();

        owner.Status = "Locked";
        if (owner.User != null)
            owner.User.Status = "locked";

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã khóa tài khoản chủ gian hàng.";
        return RedirectToAction(nameof(Index));
    }


    [HttpGet]
    public async Task<IActionResult> Requests(string? status, CancellationToken cancellationToken)
    {
        var query = _context.PoiOwnerRequests
            .Include(item => item.OwnerProfile)
                .ThenInclude(owner => owner!.User)
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(item => item.Status == status);

        ViewBag.Status = status;
        ViewBag.PendingCount = await _context.PoiOwnerRequests.CountAsync(item => item.Status == "Pending", cancellationToken);

        var requests = await query
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRequest(int id, CancellationToken cancellationToken)
    {
        var request = await _context.PoiOwnerRequests
            .Include(item => item.OwnerProfile)
            .Include(item => item.Poi)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (request == null)
            return NotFound();

        if (request.Poi == null)
        {
            TempData["ErrorMessage"] = "Yêu cầu không có POI hợp lệ.";
            return RedirectToAction(nameof(Requests));
        }

        request.Poi.OwnerProfileId = request.OwnerProfileId;

        if (request.RequestType == "Create" && request.Poi.Status == "Pending")
            request.Poi.AdminNote = "Đã xác nhận quyền chủ gian hàng. POI vẫn cần duyệt nội dung nếu chưa Approved.";

        request.Status = "Approved";
        request.ReviewedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã duyệt yêu cầu chủ gian hàng/POI.";
        return RedirectToAction(nameof(Requests));
    }

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RejectRequest(int id, string? note, CancellationToken cancellationToken)
{
    var request = await _context.PoiOwnerRequests
        .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (request == null)
        return NotFound();

    request.Status = "Rejected";
    request.ReviewedAt = DateTime.UtcNow;

    if (!string.IsNullOrWhiteSpace(note))
    {
        var adminNote = note.Trim();

        request.Note = string.IsNullOrWhiteSpace(request.Note)
            ? adminNote
            : $"{request.Note}{System.Environment.NewLine}Admin: {adminNote}";
    }

    await _context.SaveChangesAsync(cancellationToken);

    TempData["SuccessMessage"] = "Đã từ chối yêu cầu.";
    return RedirectToAction(nameof(Requests));
}

    private async Task<int> EnsureOwnerRoleAsync(CancellationToken cancellationToken)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(item => item.RoleName == "Owner", cancellationToken);
        if (role != null)
            return role.Id;

        role = new Role
        {
            RoleName = "Owner",
            Description = "Chủ gian hàng / chủ POI"
        };
        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);
        return role.Id;
    }
}
