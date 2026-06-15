using AdminWeb.Areas.Reviewer.Models;
using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Areas.Reviewer.Controllers;

[Area("Reviewer")]
[Authorize(Roles = "Reviewer")]
public sealed class DashboardController : Controller
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new ReviewerDashboardViewModel
        {
            PendingPois = await _context.Pois.CountAsync(poi => poi.Status == "Pending", cancellationToken),
            ApprovedPois = await _context.Pois.CountAsync(poi => poi.Status == "Approved", cancellationToken),
            RejectedPois = await _context.Pois.CountAsync(poi => poi.Status == "Rejected", cancellationToken),
            RecentPendingPois = await _context.Pois
                .AsNoTracking()
                .Include(poi => poi.Translations)
                .Where(poi => poi.Status == "Pending")
                .OrderByDescending(poi => poi.CreatedAt)
                .Take(8)
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }
}
