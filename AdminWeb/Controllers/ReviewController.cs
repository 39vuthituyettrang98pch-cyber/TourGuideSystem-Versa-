using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Reviewer")]
public sealed class ReviewController : Controller
{
    private const int PageSize = 15;
    private readonly AppDbContext _context;

    public ReviewController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchString, int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        searchString = searchString?.Trim();

        var query = _context.PoiReviews
            .AsNoTracking()
            .Include(item => item.Tourist)
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            query = query.Where(item =>
                (item.Comment != null && item.Comment.Contains(searchString)) ||
                (item.Tourist != null && item.Tourist.FullName != null && item.Tourist.FullName.Contains(searchString)) ||
                (item.Tourist != null && item.Tourist.Email != null && item.Tourist.Email.Contains(searchString)) ||
                (item.Poi != null && item.Poi.Translations.Any(t => t.Name.Contains(searchString))));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var reviews = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        ViewBag.SearchString = searchString;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
        ViewBag.TotalItems = totalItems;

        return View(reviews);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var review = await _context.PoiReviews
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (review == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đánh giá cần xóa.";
            return RedirectToAction(nameof(Index));
        }

        _context.PoiReviews.Remove(review);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã xóa đánh giá POI.";
        return RedirectToAction(nameof(Index));
    }
}
