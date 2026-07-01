using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class CategoryTranslationController : Controller
{
    private readonly AppDbContext _context;

    public CategoryTranslationController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId == null)
            return NotFound();

        var category = await _context.Categories
            .AsNoTracking()
            .Include(item => item.Translations)
            .FirstOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        return category == null ? NotFound() : View(category);
    }

    public async Task<IActionResult> Create(int categoryId, CancellationToken cancellationToken)
    {
        if (!await _context.Categories.AnyAsync(item => item.Id == categoryId, cancellationToken))
            return NotFound();

        await LoadLanguagesAsync(cancellationToken);
        return View(new CategoryTranslation { CategoryId = categoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CategoryTranslation translation,
        CancellationToken cancellationToken)
    {
        translation.LanguageCode = translation.LanguageCode.Trim().ToLowerInvariant();
        if (await TranslationExistsAsync(
                translation.CategoryId,
                translation.LanguageCode,
                null,
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(translation.LanguageCode),
                "Danh mục đã có bản dịch cho ngôn ngữ này.");
        }

        if (!ModelState.IsValid)
        {
            await LoadLanguagesAsync(cancellationToken);
            return View(translation);
        }

        translation.Name = translation.Name.Trim();
        _context.CategoryTranslations.Add(translation);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã thêm bản dịch danh mục.";
        return RedirectToAction(nameof(Index), new { categoryId = translation.CategoryId });
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id == null)
            return NotFound();

        var translation = await _context.CategoryTranslations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return translation == null ? NotFound() : View(translation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        CategoryTranslation input,
        CancellationToken cancellationToken)
    {
        if (id != input.Id)
            return NotFound();

        input.LanguageCode = input.LanguageCode.Trim().ToLowerInvariant();
        if (await TranslationExistsAsync(
                input.CategoryId,
                input.LanguageCode,
                input.Id,
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(input.LanguageCode),
                "Danh mục đã có bản dịch cho ngôn ngữ này.");
        }

        if (!ModelState.IsValid)
            return View(input);

        var translation = await _context.CategoryTranslations.FindAsync([id], cancellationToken);
        if (translation == null)
            return NotFound();

        translation.LanguageCode = input.LanguageCode;
        translation.Name = input.Name.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã cập nhật bản dịch danh mục.";
        return RedirectToAction(nameof(Index), new { categoryId = translation.CategoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var translation = await _context.CategoryTranslations.FindAsync([id], cancellationToken);
        if (translation == null)
            return NotFound();

        var categoryId = translation.CategoryId;
        _context.CategoryTranslations.Remove(translation);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã xóa bản dịch danh mục.";
        return RedirectToAction(nameof(Index), new { categoryId });
    }

    private async Task LoadLanguagesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Languages = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(language => language.IsActive)
            .OrderBy(language => language.LanguageCode)
            .ToListAsync(cancellationToken);
    }

    private Task<bool> TranslationExistsAsync(
        int categoryId,
        string languageCode,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        return _context.CategoryTranslations.AnyAsync(
            item => item.CategoryId == categoryId
                && item.LanguageCode == languageCode
                && (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);
    }
}
