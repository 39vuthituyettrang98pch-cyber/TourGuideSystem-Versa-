using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
public sealed class ReviewsController : Controller
{
    private readonly AppDbContext _context;

    public ReviewsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var poiIds = await _context.Pois
            .Where(poi => poi.OwnerProfileId == owner.Id)
            .Select(poi => poi.Id)
            .ToListAsync(cancellationToken);

        var reviews = await _context.PoiReviews
            .Include(review => review.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Include(review => review.Tourist)
            .Where(review => poiIds.Contains(review.PoiId))
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(reviews);
    }

    private async Task<AdminWeb.Models.OwnerProfile?> GetOwnerAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.Identity?.Name;

        return await _context.OwnerProfiles
            .Include(owner => owner.User)
            .FirstOrDefaultAsync(owner =>
                (userId != null && owner.UserId.ToString() == userId) ||
                owner.User!.Username == username,
                cancellationToken);
    }
}
