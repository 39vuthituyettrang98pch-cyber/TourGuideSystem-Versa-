using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class PoiTranslationController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public PoiTranslationController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index(int poiId, CancellationToken cancellationToken)
    {
        var poi = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .FirstOrDefaultAsync(item => item.Id == poiId, cancellationToken);
        if (poi == null)
            return NotFound();

        ViewBag.PoiId = poiId;
        ViewBag.PoiName = poi.Translations
            .FirstOrDefault(item => item.LanguageCode == "vi")?.Name
            ?? $"POI #{poiId}";
        return View(poi.Translations.OrderBy(item => item.LanguageCode).ToList());
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var translation = await _context.PoiTranslations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return translation == null ? NotFound() : View(translation);
    }

    public async Task<IActionResult> Create(int poiId, CancellationToken cancellationToken)
    {
        if (!await _context.Pois.AnyAsync(item => item.Id == poiId, cancellationToken))
            return NotFound();

        await LoadLanguagesAsync(cancellationToken);
        return View(new PoiTranslation { PoiId = poiId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PoiTranslation translation,
        IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        translation.LanguageCode = translation.LanguageCode.Trim().ToLowerInvariant();
        await ValidateAsync(translation, audioFile, null, cancellationToken);
        if (!ModelState.IsValid)
        {
            await LoadLanguagesAsync(cancellationToken);
            return View(translation);
        }

        translation.Name = translation.Name.Trim();
        translation.TtsScript = translation.FullDescription;
        translation.AudioUrl = await SaveAudioAsync(audioFile, cancellationToken);
        translation.UpdatedAt = DateTime.Now;
        _context.PoiTranslations.Add(translation);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã thêm bản dịch POI.";
        return RedirectToAction(nameof(Index), new { poiId = translation.PoiId });
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id == null)
            return NotFound();

        var translation = await _context.PoiTranslations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return translation == null ? NotFound() : View(translation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        PoiTranslation input,
        IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        if (id != input.Id)
            return NotFound();

        input.LanguageCode = input.LanguageCode.Trim().ToLowerInvariant();
        await ValidateAsync(input, audioFile, input.Id, cancellationToken);
        if (!ModelState.IsValid)
            return View(input);

        var translation = await _context.PoiTranslations.FindAsync([id], cancellationToken);
        if (translation == null)
            return NotFound();

        translation.LanguageCode = input.LanguageCode;
        translation.Name = input.Name.Trim();
        translation.ShortDescription = input.ShortDescription;
        translation.FullDescription = input.FullDescription;
        translation.TtsScript = input.FullDescription;
        translation.UpdatedAt = DateTime.Now;

        var audioUrl = await SaveAudioAsync(audioFile, cancellationToken);
        if (audioUrl != null)
            translation.AudioUrl = audioUrl;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã cập nhật bản dịch POI.";
        return RedirectToAction(nameof(Index), new { poiId = translation.PoiId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int poiId, CancellationToken cancellationToken)
    {
        var translation = await _context.PoiTranslations.FindAsync([id], cancellationToken);
        if (translation == null)
            return NotFound();

        if (translation.LanguageCode == "vi")
        {
            TempData["ErrorMessage"] = "Không thể xóa nội dung tiếng Việt gốc.";
            return RedirectToAction(nameof(Index), new { poiId });
        }

        _context.PoiTranslations.Remove(translation);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã xóa bản dịch POI.";
        return RedirectToAction(nameof(Index), new { poiId });
    }

    private async Task ValidateAsync(
        PoiTranslation translation,
        IFormFile? audioFile,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        if (!await _context.Pois.AnyAsync(item => item.Id == translation.PoiId, cancellationToken))
            ModelState.AddModelError(nameof(translation.PoiId), "POI không tồn tại.");

        if (await _context.PoiTranslations.AnyAsync(
                item => item.PoiId == translation.PoiId
                    && item.LanguageCode == translation.LanguageCode
                    && (!excludingId.HasValue || item.Id != excludingId.Value),
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(translation.LanguageCode),
                "POI đã có bản dịch cho ngôn ngữ này.");
        }

        if (audioFile is { Length: > 0 } &&
            !audioFile.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(audioFile), "File tải lên phải là audio.");
        }
    }

    private async Task LoadLanguagesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Languages = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.LanguageName)
            .ToListAsync(cancellationToken);
    }

    private async Task<string?> SaveAudioAsync(
        IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        if (audioFile is not { Length: > 0 })
            return null;

        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(webRoot, "uploads", "audio");
        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(Path.GetFileName(audioFile.FileName));
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using var stream = new FileStream(
            Path.Combine(directory, fileName),
            FileMode.CreateNew);
        await audioFile.CopyToAsync(stream, cancellationToken);
        return $"/uploads/audio/{fileName}";
    }
}
