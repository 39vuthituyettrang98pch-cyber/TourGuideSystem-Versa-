using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
[Route("AudioTasks")]
public sealed class AudioTasksController : Controller
{
    private readonly AppDbContext _context;

    public AudioTasksController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tasks = await _context.MediaTasks
            .AsNoTracking()
            .Include(task => task.Poi)
            .ThenInclude(poi => poi!.Translations)
            .OrderByDescending(task => task.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return View(tasks);
    }

    [HttpGet("Progress")]
    public async Task<IActionResult> Progress(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await _context.MediaTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);

        if (task == null)
            return NotFound(new { success = false, message = "Không tìm thấy tác vụ." });

        return Json(new
        {
            success = true,
            data = new
            {
                taskId = task.Id,
                status = task.Status.ToString(),
                percent = task.ProgressPercentage,
                task.TotalLanguages,
                task.SucceededLanguages,
                task.FailedLanguages,
                task.LastError,
                task.AttemptCount,
                task.StartedAt,
                task.CompletedAt
            }
        });
    }

    [HttpPost("Retry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await _context.MediaTasks
            .FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);

        if (task == null)
            return NotFound();

        if (task.Status is MediaTaskStatus.Pending or MediaTaskStatus.Processing)
        {
            TempData["ErrorMessage"] = "Tác vụ đang chờ hoặc đang xử lý.";
            return RedirectToAction(nameof(Index));
        }

        task.Status = MediaTaskStatus.Pending;
        task.ProgressPercentage = 0;
        task.TotalLanguages = 0;
        task.SucceededLanguages = 0;
        task.FailedLanguages = 0;
        task.LastError = null;
        task.StartedAt = null;
        task.CompletedAt = null;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã đưa tác vụ vào hàng chờ xử lý lại.";
        return RedirectToAction(nameof(Index));
    }
}
