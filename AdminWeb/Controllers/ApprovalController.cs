using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public class ApprovalController : Controller
{
    private readonly AppDbContext _context;

    public ApprovalController(AppDbContext context)
    {
        _context = context;
    }

    // GET: /Approval/PoiPending
    [HttpGet]
    public async Task<IActionResult> PoiPending()
    {
        var pendingPois = await _context.Pois
            .Include(p => p.Translations)
            .Where(p => p.Status == "Pending")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(pendingPois);
    }

    // POST: /Approval/PoiApprove/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PoiApprove(int id)
    {
        var poi = await _context.Pois.FirstOrDefaultAsync(p => p.Id == id);
        if (poi == null) return NotFound();

        poi.Status = "Approved";
        poi.AdminNote = null;
        await SyncOwnerRequestsAfterPoiReviewAsync(poi.Id, "Approved", null);
        _context.Update(poi);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã phê duyệt POI #{id}.";
        return Redirect("/Admin/Approval/PoiPending");
    }

    private async Task SyncOwnerRequestsAfterPoiReviewAsync(int poiId, string status, string? note)
    {
        var requests = await _context.PoiOwnerRequests
            .Where(item => item.PoiId == poiId && item.Status == "Pending")
            .ToListAsync();

        if (requests.Count == 0)
            return;

        var reviewedAt = DateTime.UtcNow;
        foreach (var request in requests)
        {
            request.Status = status;
            request.ReviewedAt = reviewedAt;

            if (status == "Rejected" && !string.IsNullOrWhiteSpace(note))
            {
                request.Note = string.IsNullOrWhiteSpace(request.Note)
                    ? note.Trim()
                    : $"{request.Note}{System.Environment.NewLine}Admin/Reviewer: {note.Trim()}";
            }
        }
    }

    // POST: /Approval/PoiReject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PoiReject(int id, string adminNote)
    {
        var poi = await _context.Pois.FirstOrDefaultAsync(p => p.Id == id);
        if (poi == null) return NotFound();

        poi.Status = "Rejected";
        poi.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote;
        await SyncOwnerRequestsAfterPoiReviewAsync(poi.Id, "Rejected", poi.AdminNote);
        _context.Update(poi);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã từ chối POI #{id}.";
        return Redirect("/Admin/Approval/PoiPending");
    }
}

