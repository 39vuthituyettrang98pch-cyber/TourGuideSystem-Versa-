using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
[AllowAnonymous]
public sealed class PoiMenuController : Controller
{
    private readonly AppDbContext _context;

    public PoiMenuController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int poiId, CancellationToken cancellationToken)
    {
        var poi = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.OwnerProfile)
            .FirstOrDefaultAsync(item => item.Id == poiId && item.Status == "Approved", cancellationToken);

        if (poi == null)
            return NotFound();

        var itemsRaw = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => item.PoiId == poiId && item.Status == "Active")
            .ToListAsync(cancellationToken);

        var items = itemsRaw
            .OrderBy(item => item.Price)
            .ThenBy(item => item.Name)
            .ToList();

        ViewBag.Poi = poi;
        ViewData["Title"] = "Menu / sản phẩm";
        return View(items);
    }
}
