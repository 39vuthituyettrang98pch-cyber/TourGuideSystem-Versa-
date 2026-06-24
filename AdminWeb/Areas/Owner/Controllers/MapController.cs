using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
public sealed class MapController : Controller
{
    private readonly AppDbContext _context;

    public MapController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? focusPoiId, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        if (focusPoiId.HasValue)
        {
            var ownsPoi = await _context.Pois.AnyAsync(
                poi => poi.Id == focusPoiId.Value && poi.OwnerProfileId == owner.Id,
                cancellationToken);

            ViewBag.FocusPoiId = ownsPoi ? (int?)focusPoiId.Value : null;
        }

        ViewBag.OwnerPoiCount = await _context.Pois.CountAsync(
            poi => poi.OwnerProfileId == owner.Id,
            cancellationToken);

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Data(CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return Unauthorized(new { message = "Tài khoản Owner chưa có hồ sơ gian hàng." });

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Include(poi => poi.PlaybackLogs)
            .Where(poi => poi.OwnerProfileId == owner.Id)
            .OrderBy(poi => poi.Id)
            .ToListAsync(cancellationToken);

        var data = pois.Select(poi =>
        {
            var vi = poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi");
            var first = poi.Translations.FirstOrDefault();
            var name = vi?.Name ?? first?.Name ?? $"POI #{poi.Id}";

            return new
            {
                id = poi.Id,
                name,
                shortDescription = vi?.ShortDescription ?? first?.ShortDescription ?? string.Empty,
                latitude = (double)poi.Latitude,
                longitude = (double)poi.Longitude,
                radius = poi.Radius,
                status = poi.Status,
                coverImageUrl = poi.CoverImageUrl,
                translationCount = poi.Translations.Count,
                playbackCount = poi.PlaybackLogs.Count,
                qrCodeToken = poi.QrCodeToken,
                createdAt = poi.CreatedAt.ToString("dd/MM/yyyy"),
                detailsUrl = Url.Action("Details", "Poi", new { area = "Owner", id = poi.Id }),
                menuUrl = Url.Action("Index", "MenuItems", new { area = "Owner", poiId = poi.Id })
            };
        });

        return Json(data);
    }

    private async Task<OwnerProfile?> GetOwnerAsync(CancellationToken cancellationToken)
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.Identity?.Name;
        var hasUserId = int.TryParse(userIdText, out var userId);

        return await _context.OwnerProfiles
            .Include(owner => owner.User)
            .FirstOrDefaultAsync(owner =>
                (hasUserId && owner.UserId == userId) ||
                (!string.IsNullOrWhiteSpace(username) && owner.User != null && owner.User.Username == username),
                cancellationToken);
    }
}
