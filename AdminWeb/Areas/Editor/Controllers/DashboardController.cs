using AdminWeb.Areas.Editor.Models;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Areas.Editor.Controllers;

[Area("Editor")]
[Authorize(Roles = "Editor")]
public sealed class DashboardController : Controller
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new EditorDashboardViewModel
        {
            TotalPois = await _context.Pois.CountAsync(cancellationToken),
            PendingPois = await _context.Pois.CountAsync(
                poi => poi.Status == "Pending",
                cancellationToken),
            TotalTours = await _context.Tours.CountAsync(cancellationToken),
            TotalCategories = await _context.Categories.CountAsync(cancellationToken),
            PendingMediaTasks = await _context.MediaTasks.CountAsync(
                task => task.Status == MediaTaskStatus.Pending || task.Status == MediaTaskStatus.Processing,
                cancellationToken),
            FailedMediaTasks = await _context.MediaTasks.CountAsync(
                task => task.Status == MediaTaskStatus.Failed || task.Status == MediaTaskStatus.CompletedWithErrors,
                cancellationToken),
            RecentPois = await _context.Pois
                .AsNoTracking()
                .Include(poi => poi.Translations)
                .OrderByDescending(poi => poi.CreatedAt)
                .Take(6)
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }
}
