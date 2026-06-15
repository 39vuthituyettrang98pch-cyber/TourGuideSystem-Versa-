using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class TourTranslationController : Controller
{
    private readonly AppDbContext _context;

    public TourTranslationController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? tourId, CancellationToken cancellationToken)
    {
        if (tourId == null)
            return NotFound();

        var tour = await _context.Tours
            .AsNoTracking()
            .Include(item => item.Translations)
            .FirstOrDefaultAsync(item => item.Id == tourId, cancellationToken);
        return tour == null ? NotFound() : View(tour);
    }

    public async Task<IActionResult> Create(int tourId, CancellationToken cancellationToken)
    {
        if (!await _context.Tours.AnyAsync(item => item.Id == tourId, cancellationToken))
            return NotFound();

        return View(new TourTranslation { TourId = tourId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        TourTranslation translation,
        CancellationToken cancellationToken)
    {
        translation.LanguageCode = translation.LanguageCode.Trim().ToLowerInvariant();
        if (await TranslationExistsAsync(
                translation.TourId,
                translation.LanguageCode,
                null,
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(translation.LanguageCode),
                "Tour đã có bản dịch cho ngôn ngữ này.");
        }

        if (!ModelState.IsValid)
            return View(translation);

        translation.Title = translation.Title.Trim();
        _context.TourTranslations.Add(translation);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã thêm bản dịch tour.";
        return RedirectToAction(nameof(Index), new { tourId = translation.TourId });
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id == null)
            return NotFound();

        var translation = await _context.TourTranslations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return translation == null ? NotFound() : View(translation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        TourTranslation input,
        CancellationToken cancellationToken)
    {
        if (id != input.Id)
            return NotFound();

        input.LanguageCode = input.LanguageCode.Trim().ToLowerInvariant();
        if (await TranslationExistsAsync(
                input.TourId,
                input.LanguageCode,
                input.Id,
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(input.LanguageCode),
                "Tour đã có bản dịch cho ngôn ngữ này.");
        }

        if (!ModelState.IsValid)
            return View(input);

        var translation = await _context.TourTranslations.FindAsync([id], cancellationToken);
        if (translation == null)
            return NotFound();

        translation.LanguageCode = input.LanguageCode;
        translation.Title = input.Title.Trim();
        translation.Description = input.Description;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã cập nhật bản dịch tour.";
        return RedirectToAction(nameof(Index), new { tourId = translation.TourId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var translation = await _context.TourTranslations.FindAsync([id], cancellationToken);
        if (translation == null)
            return NotFound();

        var tourId = translation.TourId;
        _context.TourTranslations.Remove(translation);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã xóa bản dịch tour.";
        return RedirectToAction(nameof(Index), new { tourId });
    }

    private Task<bool> TranslationExistsAsync(
        int tourId,
        string languageCode,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        return _context.TourTranslations.AnyAsync(
            item => item.TourId == tourId
                && item.LanguageCode == languageCode
                && (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);
    }
}
