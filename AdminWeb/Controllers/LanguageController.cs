using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class LanguageController : Controller
{
    private readonly AppDbContext _context;

    public LanguageController(AppDbContext context)
    {
        _context = context;
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

        language.LanguageName = language.LanguageName.Trim();
        language.EdgeTtsVoice = language.EdgeTtsVoice.Trim();
        _context.SupportedLanguages.Add(language);
        await _context.SaveChangesAsync(cancellationToken);
        var queuedTasks = language.IsActive
            ? await QueueMissingTranslationTasksAsync(language.LanguageCode, cancellationToken)
            : 0;
        TempData["SuccessMessage"] = BuildSuccessMessage(
            "Đã thêm ngôn ngữ hỗ trợ.",
            queuedTasks);
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
        if (!string.Equals(id, input.LanguageCode, StringComparison.OrdinalIgnoreCase))
            return NotFound();
        if (!ModelState.IsValid)
            return View(input);

        var language = await _context.SupportedLanguages.FindAsync([id], cancellationToken);
        if (language == null)
            return NotFound();

        var wasActive = language.IsActive;
        language.LanguageName = input.LanguageName.Trim();
        language.EdgeTtsVoice = input.EdgeTtsVoice.Trim();
        language.IsActive = input.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        var queuedTasks = !wasActive && language.IsActive
            ? await QueueMissingTranslationTasksAsync(language.LanguageCode, cancellationToken)
            : 0;
        TempData["SuccessMessage"] = BuildSuccessMessage(
            "Đã cập nhật ngôn ngữ và giọng đọc.",
            queuedTasks);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string id, CancellationToken cancellationToken)
    {
        var language = await _context.SupportedLanguages.FindAsync([id], cancellationToken);
        if (language == null)
            return NotFound();

        language.IsActive = !language.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        var queuedTasks = language.IsActive
            ? await QueueMissingTranslationTasksAsync(language.LanguageCode, cancellationToken)
            : 0;
        TempData["SuccessMessage"] = language.IsActive
            ? BuildSuccessMessage("Đã bật ngôn ngữ.", queuedTasks)
            : "Đã tắt ngôn ngữ.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<int> QueueMissingTranslationTasksAsync(
        string languageCode,
        CancellationToken cancellationToken)
    {
        if (string.Equals(languageCode, "vi", StringComparison.OrdinalIgnoreCase))
            return 0;

        var pois = await _context.Pois
            .Include(poi => poi.Translations)
            .Include(poi => poi.MediaTasks)
            .Where(poi =>
                !poi.Translations.Any(translation =>
                    translation.LanguageCode == languageCode) &&
                !poi.MediaTasks.Any(task =>
                    task.Status == MediaTaskStatus.Pending ||
                    task.Status == MediaTaskStatus.Processing))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var poi in pois)
        {
            var vietnameseSource = poi.Translations.FirstOrDefault(translation =>
                translation.LanguageCode == "vi");
            if (vietnameseSource == null)
                continue;

            poi.MediaTasks.Add(new MediaTask
            {
                TaskType = string.IsNullOrWhiteSpace(vietnameseSource.VideoUrl)
                    ? MediaTaskType.TextToAudio
                    : MediaTaskType.VideoDubbing,
                Status = MediaTaskStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return pois.Count(poi =>
            poi.Translations.Any(translation => translation.LanguageCode == "vi"));
    }

    private static string BuildSuccessMessage(string message, int queuedTasks)
    {
        return queuedTasks > 0
            ? $"{message} Đã đưa {queuedTasks} POI còn thiếu bản dịch vào hàng đợi AI."
            : message;
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToLowerInvariant();
    }
}
