using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
[Route("AudioTasks")]
[Route("Admin/AudioTasks")]
[Route("Editor/AudioTasks")]
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
        var portalRedirect = RedirectToRoleSpecificAudioTasksPath();
        if (portalRedirect != null)
            return portalRedirect;

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
            return RedirectToCurrentPortalIndex();
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
        return RedirectToCurrentPortalIndex();
    }

    [HttpPost("QueueMissingTextToAudio")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QueueMissingTextToAudio(CancellationToken cancellationToken)
    {
        var activeLanguageCodes = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(language => language.IsActive)
            .Select(language => language.LanguageCode)
            .ToListAsync(cancellationToken);

        if (activeLanguageCodes.Count == 0)
        {
            TempData["ErrorMessage"] = "Chưa có ngôn ngữ nào đang bật.";
            return RedirectToCurrentPortalIndex();
        }

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Where(poi => poi.Status == "Approved" || poi.Status == "active" || poi.Status == "Active")
            .OrderBy(poi => poi.Id)
            .ToListAsync(cancellationToken);

        if (pois.Count == 0)
        {
            TempData["ErrorMessage"] = "Chưa có POI đã duyệt để tạo audio.";
            return RedirectToCurrentPortalIndex();
        }

        var runningPoiIds = await _context.MediaTasks
            .AsNoTracking()
            .Where(task =>
                task.TaskType == MediaTaskType.TextToAudio &&
                (task.Status == MediaTaskStatus.Pending || task.Status == MediaTaskStatus.Processing))
            .Select(task => task.PoiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var runningPoiIdSet = runningPoiIds.ToHashSet();
        var queuedCount = 0;
        var skippedNoVietnameseSource = 0;
        var skippedAlreadyRunning = 0;
        var skippedAlreadyEnoughAudio = 0;
        var now = DateTime.UtcNow;

        foreach (var poi in pois)
        {
            if (runningPoiIdSet.Contains(poi.Id))
            {
                skippedAlreadyRunning++;
                continue;
            }

            var vietnameseSource = poi.Translations.FirstOrDefault(translation =>
                translation.LanguageCode == "vi" &&
                !string.IsNullOrWhiteSpace(translation.FullDescription));

            if (vietnameseSource == null)
            {
                skippedNoVietnameseSource++;
                continue;
            }

            var hasMissingAudio = activeLanguageCodes.Any(languageCode =>
            {
                var translation = poi.Translations.FirstOrDefault(item =>
                    string.Equals(item.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase));

                return translation == null || string.IsNullOrWhiteSpace(translation.AudioUrl);
            });

            if (!hasMissingAudio)
            {
                skippedAlreadyEnoughAudio++;
                continue;
            }

            _context.MediaTasks.Add(new MediaTask
            {
                Id = Guid.NewGuid(),
                PoiId = poi.Id,
                TaskType = MediaTaskType.TextToAudio,
                Status = MediaTaskStatus.Pending,
                ProgressPercentage = 0,
                TotalLanguages = activeLanguageCodes.Count,
                SucceededLanguages = 0,
                FailedLanguages = 0,
                AttemptCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            queuedCount++;
        }

        if (queuedCount > 0)
            await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] =
            $"Đã tạo {queuedCount} tác vụ TextToAudio cho các POI còn thiếu audio. " +
            $"Bỏ qua {skippedAlreadyRunning} POI đang chạy, " +
            $"{skippedAlreadyEnoughAudio} POI đã đủ audio, " +
            $"{skippedNoVietnameseSource} POI thiếu nội dung tiếng Việt.";

        return RedirectToCurrentPortalIndex();
    }

    [HttpPost("RegenerateAllTextToAudio")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateAllTextToAudio(CancellationToken cancellationToken)
    {
        var activeLanguageCount = await _context.SupportedLanguages
            .AsNoTracking()
            .CountAsync(language => language.IsActive, cancellationToken);

        if (activeLanguageCount == 0)
        {
            TempData["ErrorMessage"] = "Chưa có ngôn ngữ nào đang bật.";
            return RedirectToCurrentPortalIndex();
        }

        var poiIds = await _context.Pois
            .AsNoTracking()
            .Where(poi => poi.Status == "Approved" || poi.Status == "active" || poi.Status == "Active")
            .Where(poi => poi.Translations.Any(translation =>
                translation.LanguageCode == "vi" &&
                !string.IsNullOrWhiteSpace(translation.FullDescription)))
            .Select(poi => poi.Id)
            .ToListAsync(cancellationToken);

        var runningPoiIds = await _context.MediaTasks
            .AsNoTracking()
            .Where(task =>
                task.TaskType == MediaTaskType.TextToAudio &&
                (task.Status == MediaTaskStatus.Pending || task.Status == MediaTaskStatus.Processing))
            .Select(task => task.PoiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var runningPoiIdSet = runningPoiIds.ToHashSet();
        var now = DateTime.UtcNow;
        var queuedCount = 0;

        foreach (var poiId in poiIds)
        {
            if (runningPoiIdSet.Contains(poiId))
                continue;

            _context.MediaTasks.Add(new MediaTask
            {
                Id = Guid.NewGuid(),
                PoiId = poiId,
                TaskType = MediaTaskType.TextToAudio,
                Status = MediaTaskStatus.Pending,
                ProgressPercentage = 0,
                TotalLanguages = activeLanguageCount,
                SucceededLanguages = 0,
                FailedLanguages = 0,
                AttemptCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            queuedCount++;
        }

        if (queuedCount > 0)
            await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Đã đưa {queuedCount} POI vào hàng chờ tạo lại audio cho toàn bộ ngôn ngữ active.";
        return RedirectToCurrentPortalIndex();
    }

    private IActionResult? RedirectToRoleSpecificAudioTasksPath()
    {
        var path = Request.Path;

        if (path.StartsWithSegments("/Admin/AudioTasks"))
        {
            if (!User.IsInRole("Admin"))
                return Redirect("/Admin/Login");

            return null;
        }

        if (path.StartsWithSegments("/Editor/AudioTasks"))
        {
            if (!User.IsInRole("Editor"))
                return User.IsInRole("Admin")
                    ? Redirect("/Admin/AudioTasks")
                    : Redirect("/Editor/Login");

            return null;
        }

        if (User.IsInRole("Admin"))
            return Redirect("/Admin/AudioTasks");

        if (User.IsInRole("Editor"))
            return Redirect("/Editor/AudioTasks");

        return Redirect("/Admin/Login");
    }

    private IActionResult RedirectToCurrentPortalIndex()
    {
        if (Request.Path.StartsWithSegments("/Editor/AudioTasks"))
            return Redirect("/Editor/AudioTasks");

        return Redirect("/Admin/AudioTasks");
    }

}
