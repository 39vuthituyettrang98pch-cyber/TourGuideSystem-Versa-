using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
public sealed class MenuOrdersController : Controller
{
    private readonly AppDbContext _context;

    public MenuOrdersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var query = _context.MenuOrders
            .AsNoTracking()
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Include(item => item.Tourist)
            .Include(item => item.Items)
            .Where(item => item.OwnerProfileId == owner.Id);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(item => item.Status == status);

        var orders = await query
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        ViewBag.Status = status;
        ViewData["Title"] = "Đơn mua menu";
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var order = await _context.MenuOrders
            .AsNoTracking()
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Include(item => item.Tourist)
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id && item.OwnerProfileId == owner.Id, cancellationToken);

        if (order == null)
            return NotFound();

        ViewData["Title"] = $"Đơn {order.OrderCode}";
        return View(order);
    }

    [HttpPost("/Owner/MenuOrders/UpdateStatus")]
    [HttpPost("/Owner/MenuOrders/Details/{id:int}/UpdateStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var order = await _context.MenuOrders
            .FirstOrDefaultAsync(item => item.Id == id && item.OwnerProfileId == owner.Id, cancellationToken);

        if (order == null)
            return NotFound();

        var next = NormalizeStatus(status);
        if (next == null)
        {
            TempData["OwnerErrorMessage"] = "Trạng thái đơn không hợp lệ.";
            return RedirectToAction(nameof(Details), "MenuOrders", new { area = "Owner", id });
        }

        var now = DateTime.UtcNow;
        order.Status = next;

        if (next == "Confirmed" && order.ConfirmedAt == null)
            order.ConfirmedAt = now;

        if (next == "Completed")
        {
            order.CompletedAt = now;
            order.PaymentStatus = "Paid";
        }
        else if (order.Status != "Completed")
        {
            // Keep the payment state consistent when an order is moved away from Completed.
            if (order.PaymentStatus == "Paid")
                order.PaymentStatus = "Unpaid";
        }

        if (next == "Cancelled")
        {
            order.CancelledAt = now;
            if (order.PaymentStatus == "Paid")
                order.PaymentStatus = "Refunded";
        }

        await _context.SaveChangesAsync(cancellationToken);
        TempData["OwnerSuccessMessage"] = "Đã cập nhật trạng thái đơn hàng.";
        return RedirectToAction(nameof(Details), "MenuOrders", new { area = "Owner", id });
    }

    private static string? NormalizeStatus(string? status)
    {
        return status?.Trim() switch
        {
            "Pending" => "Pending",
            "Confirmed" => "Confirmed",
            "Preparing" => "Preparing",
            "Ready" => "Ready",
            "Completed" => "Completed",
            "Cancelled" => "Cancelled",
            _ => null
        };
    }

    private async Task<OwnerProfile?> GetOwnerAsync(CancellationToken cancellationToken)
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
