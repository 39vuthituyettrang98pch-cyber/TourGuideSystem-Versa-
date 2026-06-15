using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class VisitorController : Controller
{
    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly VisitorAchievementService _achievementService;

    public VisitorController(
        AppDbContext context,
        PasswordService passwordService,
        VisitorAchievementService achievementService)
    {
        _context = context;
        _passwordService = passwordService;
        _achievementService = achievementService;
    }

    public async Task<IActionResult> Index(string? searchString, CancellationToken cancellationToken)
    {
        ViewData["CurrentFilter"] = searchString;
        return View(await _achievementService.GetVisitorsAsync(searchString, cancellationToken));
    }

    public async Task<IActionResult> Details(int? id, CancellationToken cancellationToken)
    {
        if (id == null)
            return NotFound();

        var details = await _achievementService.GetDetailsAsync(id.Value, cancellationToken);
        return details == null ? NotFound() : View(details);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Tourist tourist, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            ModelState.AddModelError(nameof(password), "Mật khẩu phải có ít nhất 8 ký tự.");

        if (!ModelState.IsValid)
            return View(tourist);

        tourist.PasswordHash = _passwordService.Hash(password);
        tourist.CreatedAt = DateTime.Now;
        _context.Add(tourist);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Thêm du khách mới thành công!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var tourist = await _context.Tourists.FindAsync(id);
        return tourist == null ? NotFound() : View(tourist);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Tourist tourist, string? newPassword)
    {
        if (id != tourist.Id)
            return NotFound();
        if (!ModelState.IsValid)
            return View(tourist);

        var existingTourist = await _context.Tourists.FindAsync(id);
        if (existingTourist == null)
            return NotFound();

        existingTourist.FullName = tourist.FullName;
        existingTourist.Email = tourist.Email;
        if (!string.IsNullOrEmpty(newPassword))
            existingTourist.PasswordHash = _passwordService.Hash(newPassword);

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Cập nhật thông tin du khách thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var tourist = await _context.Tourists.FindAsync(id);
        if (tourist != null)
        {
            _context.Tourists.Remove(tourist);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa du khách thành công!";
        }

        return RedirectToAction(nameof(Index));
    }
}
