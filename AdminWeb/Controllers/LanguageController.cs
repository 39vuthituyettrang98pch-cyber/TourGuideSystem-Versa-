using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class LanguageController : Controller
{
    private readonly AppDbContext _context;
    private readonly ContentTranslationService _contentTranslationService;

    public LanguageController(
        AppDbContext context,
        ContentTranslationService contentTranslationService)
    {
        _context = context;
        _contentTranslationService = contentTranslationService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var languages = await _context.SupportedLanguages
            .AsNoTracking()
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.LanguageCode)
            .ToListAsync(cancellationToken);

        return View(languages);
    }

    public IActionResult Create()
    {
        return View(new SupportedLanguage { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        SupportedLanguage language,
        CancellationToken cancellationToken)
    {
        language.LanguageCode = NormalizeCode(language.LanguageCode);

        if (await _context.SupportedLanguages.AnyAsync(
                item => item.LanguageCode == language.LanguageCode,
                cancellationToken))
        {
            ModelState.AddModelError(nameof(language.LanguageCode), "Mã ngôn ngữ đã tồn tại.");
        }

        if (!ModelState.IsValid)
            return View(language);

        language.LanguageName = (language.LanguageName ?? string.Empty).Trim();
        language.EdgeTtsVoice = (language.EdgeTtsVoice ?? string.Empty).Trim();

        _context.SupportedLanguages.Add(language);
        await _context.SaveChangesAsync(cancellationToken);

        var queuedTasks = 0;
        var translatedTours = 0;
        var translatedCategories = 0;

        if (language.IsActive)
        {
            queuedTasks = await QueueMissingTranslationTasksAsync(language.LanguageCode, cancellationToken);

            try
            {
                var summary = await _contentTranslationService
                    .TranslateMissingContentForLanguageAsync(language.LanguageCode, cancellationToken);

                translatedTours = summary.Tours;
                translatedCategories = summary.Categories;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    "Ngôn ngữ đã được thêm, nhưng AI chưa dịch được tour/danh mục: " + ex.Message;
            }
        }

        TempData["SuccessMessage"] = BuildSuccessMessage(
            "Đã thêm ngôn ngữ hỗ trợ.",
            queuedTasks,
            translatedTours,
            translatedCategories);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var language = await _context.SupportedLanguages
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.LanguageCode == id, cancellationToken);

        return language == null ? NotFound() : View(language);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        SupportedLanguage input,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeCode(id);
        input.LanguageCode = NormalizeCode(input.LanguageCode);

        if (!string.Equals(normalizedId, input.LanguageCode, StringComparison.OrdinalIgnoreCase))
            return NotFound();

        if (!ModelState.IsValid)
            return View(input);

        var language = await _context.SupportedLanguages.FindAsync([normalizedId], cancellationToken);
        if (language == null)
            return NotFound();

        var wasActive = language.IsActive;

        language.LanguageName = (input.LanguageName ?? string.Empty).Trim();
        language.EdgeTtsVoice = (input.EdgeTtsVoice ?? string.Empty).Trim();
        language.IsActive = input.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        var queuedTasks = 0;
        var translatedTours = 0;
        var translatedCategories = 0;

        if (!wasActive && language.IsActive)
        {
            queuedTasks = await QueueMissingTranslationTasksAsync(language.LanguageCode, cancellationToken);

            try
            {
                var summary = await _contentTranslationService
                    .TranslateMissingContentForLanguageAsync(language.LanguageCode, cancellationToken);

                translatedTours = summary.Tours;
                translatedCategories = summary.Categories;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    "Ngôn ngữ đã được bật, nhưng AI chưa dịch được tour/danh mục: " + ex.Message;
            }
        }

        TempData["SuccessMessage"] = BuildSuccessMessage(
            "Đã cập nhật ngôn ngữ và giọng đọc.",
            queuedTasks,
            translatedTours,
            translatedCategories);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string id, CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeCode(id);
        var language = await _context.SupportedLanguages.FindAsync([normalizedId], cancellationToken);
        if (language == null)
            return NotFound();

        language.IsActive = !language.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        var queuedTasks = 0;
        var translatedTours = 0;
        var translatedCategories = 0;

        if (language.IsActive)
        {
            queuedTasks = await QueueMissingTranslationTasksAsync(language.LanguageCode, cancellationToken);

            try
            {
                var summary = await _contentTranslationService
                    .TranslateMissingContentForLanguageAsync(language.LanguageCode, cancellationToken);

                translatedTours = summary.Tours;
                translatedCategories = summary.Categories;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    "Ngôn ngữ đã được bật, nhưng AI chưa dịch được tour/danh mục: " + ex.Message;
            }
        }

        TempData["SuccessMessage"] = language.IsActive
            ? BuildSuccessMessage("Đã bật ngôn ngữ.", queuedTasks, translatedTours, translatedCategories)
            : "Đã tắt ngôn ngữ.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<int> QueueMissingTranslationTasksAsync(
        string languageCode,
        CancellationToken cancellationToken)
    {
        languageCode = NormalizeCode(languageCode);

        if (string.Equals(languageCode, "vi", StringComparison.OrdinalIgnoreCase))
            return 0;

        // Quan trọng: không dùng Include(...).Add(...) vào navigation cũ.
        // Cách đó dễ làm EF update nhầm entity đã bị thay đổi/xóa và gây DbUpdateConcurrencyException.
        // Đoạn này chỉ query AsNoTracking rồi insert task mới trực tiếp vào bảng media_tasks.
        _context.ChangeTracker.Clear();

        var candidatePoiIds = await _context.Pois
            .AsNoTracking()
            .Where(poi =>
                poi.Translations.Any(translation => translation.LanguageCode == "vi") &&
                !poi.Translations.Any(translation => translation.LanguageCode == languageCode))
            .Select(poi => poi.Id)
            .ToListAsync(cancellationToken);

        if (candidatePoiIds.Count == 0)
            return 0;

        var busyPoiIds = await _context.MediaTasks
            .AsNoTracking()
            .Where(task =>
                candidatePoiIds.Contains(task.PoiId) &&
                (task.Status == MediaTaskStatus.Pending || task.Status == MediaTaskStatus.Processing))
            .Select(task => task.PoiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var busySet = busyPoiIds.ToHashSet();
        var queuePoiIds = candidatePoiIds
            .Where(poiId => !busySet.Contains(poiId))
            .Distinct()
            .ToList();

        if (queuePoiIds.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        var tasks = queuePoiIds.Select(poiId => new MediaTask
        {
            Id = Guid.NewGuid(),
            PoiId = poiId,
            TaskType = MediaTaskType.TextToAudio,
            Status = MediaTaskStatus.Pending,
            ProgressPercentage = 0,
            TotalLanguages = 0,
            SucceededLanguages = 0,
            FailedLanguages = 0,
            AttemptCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        await _context.MediaTasks.AddRangeAsync(tasks, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return tasks.Count;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Không để việc tạo hàng đợi AI làm vỡ chức năng thêm/bật ngôn ngữ.
            // Ngôn ngữ vẫn đã được lưu; admin có thể chạy lại tác vụ AI media sau.
            _context.ChangeTracker.Clear();
            return 0;
        }
    }

    private static string BuildSuccessMessage(
        string message,
        int queuedTasks,
        int translatedTours = 0,
        int translatedCategories = 0)
    {
        var parts = new List<string> { message };

        if (queuedTasks > 0)
            parts.Add($"Đã đưa {queuedTasks} POI còn thiếu bản dịch/audio vào hàng đợi AI.");

        if (translatedTours > 0)
            parts.Add($"Đã AI dịch thêm {translatedTours} bản dịch tour.");

        if (translatedCategories > 0)
            parts.Add($"Đã AI dịch thêm {translatedCategories} bản dịch danh mục.");

        return string.Join(" ", parts);
    }

    private static string NormalizeCode(string? code)
    {
        return (code ?? string.Empty).Trim().ToLowerInvariant();
    }
}
