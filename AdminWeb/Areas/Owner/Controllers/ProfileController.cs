using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
public sealed class ProfileController : Controller
{
    private readonly AppDbContext _context;

    public ProfileController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction(nameof(Create), "Profile", new { area = "Owner" });

        return View(owner);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new OwnerProfile { Status = "Pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OwnerProfile model, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user == null)
            return RedirectToAction("Login", "Account", new { area = "Owner" });

        var exists = await _context.OwnerProfiles.AnyAsync(item => item.UserId == user.Id, cancellationToken);
        if (exists)
            return RedirectToAction(nameof(Index), "Profile", new { area = "Owner" });

        model.UserId = user.Id;
        model.Status = "Pending";
        model.CreatedAt = DateTime.UtcNow;
        _context.OwnerProfiles.Add(model);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã gửi hồ sơ chủ gian hàng. Vui lòng chờ admin duyệt.";
        return RedirectToAction(nameof(Index), "Profile", new { area = "Owner" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string businessName, string? representativeName, string? phone, string? address, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction(nameof(Create), "Profile", new { area = "Owner" });

        owner.BusinessName = (businessName ?? string.Empty).Trim();
        owner.RepresentativeName = representativeName;
        owner.Phone = phone;
        owner.Address = address;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã cập nhật hồ sơ gian hàng.";
        return RedirectToAction(nameof(Index), "Profile", new { area = "Owner" });
    }

    private async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.Identity?.Name;

        return await _context.Users.FirstOrDefaultAsync(user =>
            (userId != null && user.Id.ToString() == userId) || user.Username == username,
            cancellationToken);
    }

    private async Task<OwnerProfile?> GetOwnerAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user == null)
            return null;

        return await _context.OwnerProfiles
            .Include(owner => owner.User)
            .FirstOrDefaultAsync(owner => owner.UserId == user.Id, cancellationToken);
    }
}
