using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
public sealed class DashboardController : Controller
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
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

        await NormalizeCompletedRequestsAsync(owner.Id, cancellationToken);

        ViewBag.PoiCount = poiIds.Count;
        ViewBag.ReviewCount = await _context.PoiReviews.CountAsync(review => poiIds.Contains(review.PoiId), cancellationToken);
        ViewBag.PlayCount = await _context.VisitorPlaybackLogs.CountAsync(log => poiIds.Contains(log.PoiId), cancellationToken);
        ViewBag.PendingPayments = await _context.PaymentTransactions.CountAsync(payment => payment.OwnerProfileId == owner.Id && payment.Status == "Pending", cancellationToken);
        ViewBag.MenuItemCount = await _context.OwnerMenuItems.CountAsync(item => item.OwnerProfileId == owner.Id, cancellationToken);
        ViewBag.PendingRequestCount = await _context.PoiOwnerRequests.CountAsync(item => item.OwnerProfileId == owner.Id && item.Status == "Pending", cancellationToken);
        ViewBag.ActiveSubscription = await _context.OwnerSubscriptions
            .Include(item => item.PaymentPlan)
            .Where(item => item.OwnerProfileId == owner.Id && item.Status == "Active")
            .OrderByDescending(item => item.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        return View(owner);
    }

    private async Task NormalizeCompletedRequestsAsync(int ownerId, CancellationToken cancellationToken)
    {
        var completedRequests = await _context.PoiOwnerRequests
            .Include(item => item.Poi)
            .Where(item =>
                item.OwnerProfileId == ownerId &&
                item.Status == "Pending" &&
                item.Poi != null &&
                item.Poi.OwnerProfileId == ownerId &&
                item.Poi.Status == "Approved")
            .ToListAsync(cancellationToken);

        if (completedRequests.Count == 0)
            return;

        var reviewedAt = DateTime.UtcNow;
        foreach (var request in completedRequests)
        {
            request.Status = "Approved";
            request.ReviewedAt ??= reviewedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
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
