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
            return RedirectToAction(nameof(Index));
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
            return RedirectToAction(nameof(Index));
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

        return RedirectToAction(nameof(Index));
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
            return RedirectToAction(nameof(Index));
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
        return RedirectToAction(nameof(Index));
    }

}
