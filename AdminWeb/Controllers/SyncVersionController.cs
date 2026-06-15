using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class SyncVersionController : Controller
{
    private readonly AppDbContext _context;
    private readonly SyncPackageService _packageService;

    public SyncVersionController(AppDbContext context, SyncPackageService packageService)
    {
        _context = context;
        _packageService = packageService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var versions = await _context.SyncVersions
            .AsNoTracking()
            .OrderByDescending(version => version.CreatedAt)
            .ToListAsync(cancellationToken);
        return View(versions);
    }

    public IActionResult Create()
    {
        return View(new SyncVersion
        {
            VersionNumber = $"v{DateTime.Now:yyyyMMdd.HHmm}"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        SyncVersion syncVersion,
        CancellationToken cancellationToken)
    {
        syncVersion.VersionNumber = syncVersion.VersionNumber.Trim();
        if (await _context.SyncVersions.AnyAsync(
                item => item.VersionNumber == syncVersion.VersionNumber,
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(syncVersion.VersionNumber),
                "Mã phiên bản đã tồn tại.");
        }

        if (!ModelState.IsValid)
            return View(syncVersion);

        syncVersion.CreatedAt = DateTime.Now;
        _context.SyncVersions.Add(syncVersion);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _packageService.GenerateAsync(syncVersion, cancellationToken);
        }
        catch
        {
            _context.SyncVersions.Remove(syncVersion);
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }

        TempData["SuccessMessage"] =
            $"Đã tạo và đóng gói phiên bản {syncVersion.VersionNumber}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var version = await _context.SyncVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (version == null)
            return NotFound();

        var path = _packageService.FindPackagePath(version)
            ?? await _packageService.GenerateAsync(version, cancellationToken);
        return PhysicalFile(
            path,
            "application/json",
            $"versa-sync-{version.VersionNumber}.json");
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        int id,
        CancellationToken cancellationToken)
    {
        var version = await _context.SyncVersions.FindAsync([id], cancellationToken);
        if (version == null)
            return NotFound();

        _packageService.DeletePackage(version);
        _context.SyncVersions.Remove(version);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã xóa phiên bản đồng bộ.";
        return RedirectToAction(nameof(Index));
    }
}
