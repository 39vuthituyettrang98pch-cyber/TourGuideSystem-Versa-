using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class CategoryController : Controller
{
    private readonly AppDbContext _context;

    public CategoryController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Include(category => category.Translations)
            .OrderBy(category => category.Id)
            .ToListAsync(cancellationToken);
        return View(categories);
    }

    public IActionResult Create()
    {
        return View(new Category());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Category category,
        string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError("Name", "Vui lòng nhập tên danh mục.");

        if (!ModelState.IsValid)
            return View(category);

        category.Translations.Add(new CategoryTranslation
        {
            LanguageCode = "vi",
            Name = name.Trim()
        });
        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã thêm danh mục.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id == null)
            return NotFound();

        var category = await _context.Categories.FindAsync([id.Value], cancellationToken);
        return category == null ? NotFound() : View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        Category input,
        CancellationToken cancellationToken)
    {
        if (id != input.Id)
            return NotFound();

        var category = await _context.Categories.FindAsync([id], cancellationToken);
        if (category == null)
            return NotFound();

        if (!ModelState.IsValid)
            return View(input);

        category.IconUrl = input.IconUrl;
        category.Status = input.Status;
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã cập nhật danh mục.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var category = await _context.Categories.FindAsync([id], cancellationToken);
        if (category == null)
            return NotFound();

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã xóa danh mục.";
        return RedirectToAction(nameof(Index));
    }
}
