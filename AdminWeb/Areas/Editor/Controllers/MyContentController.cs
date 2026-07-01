using AdminWeb.Areas.Editor.Models;
using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Editor.Controllers;

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class MyContentController : Controller
{
    private readonly AppDbContext _context;

    public MyContentController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string status = "All", string? q = null, CancellationToken cancellationToken = default)
    {
        var editorId = GetCurrentUserId();
        var editorUserId = editorId ?? -1;
        var keyword = q?.Trim();

        var poiQuery = _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Include(poi => poi.OwnerProfile)
            .Where(poi => poi.CreatedBy == editorUserId);

        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            poiQuery = poiQuery.Where(poi => poi.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var like = $"%{keyword}%";
            if (int.TryParse(keyword.TrimStart('#'), out var id))
            {
                poiQuery = poiQuery.Where(poi => poi.Id == id || poi.Translations.Any(translation => EF.Functions.Like(translation.Name, like)));
            }
            else
            {
                poiQuery = poiQuery.Where(poi => poi.Translations.Any(translation =>
                    EF.Functions.Like(translation.Name, like) ||
                    (translation.ShortDescription != null && EF.Functions.Like(translation.ShortDescription, like))));
            }
        }

        var allMyPoiStatuses = await _context.Pois
            .AsNoTracking()
            .Where(poi => poi.CreatedBy == editorUserId)
            .GroupBy(poi => poi.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var pois = await poiQuery
            .OrderByDescending(poi => poi.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var poiIds = pois.Select(poi => poi.Id).ToList();
        var menuCounts = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => poiIds.Contains(item.PoiId))
            .GroupBy(item => item.PoiId)
            .Select(group => new { PoiId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PoiId, item => item.Count, cancellationToken);

        var createdTourIds = await _context.AdminActivityLogs
            .AsNoTracking()
            .Where(log =>
                log.UserId == editorUserId &&
                log.TargetTable == "tours" &&
                log.TargetId != null &&
                (log.Action == "EditorCreateTour" || log.Action == "CreateTour" || log.Action == "AdminCreateTour"))
            .GroupBy(log => log.TargetId!.Value)
            .Select(group => new { TourId = group.Key, LoggedAt = group.Max(log => log.CreatedAt) })
            .ToListAsync(cancellationToken);

        var createdTourIdList = createdTourIds.Select(item => item.TourId).ToList();
        var loggedAtByTourId = createdTourIds.ToDictionary(item => item.TourId, item => (DateTime?)item.LoggedAt);

        var tours = await _context.Tours
            .AsNoTracking()
            .Include(tour => tour.Translations)
            .Include(tour => tour.TourPois)
            .Where(tour => createdTourIdList.Contains(tour.Id))
            .OrderByDescending(tour => tour.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var model = new EditorMyContentViewModel
        {
            Search = q,
            StatusFilter = status,
            TotalPoiCount = allMyPoiStatuses.Sum(item => item.Count),
            DraftPoiCount = allMyPoiStatuses.Where(item => item.Status == "Draft").Sum(item => item.Count),
            PendingPoiCount = allMyPoiStatuses.Where(item => item.Status == "Pending").Sum(item => item.Count),
            ApprovedPoiCount = allMyPoiStatuses.Where(item => item.Status == "Approved").Sum(item => item.Count),
            RejectedPoiCount = allMyPoiStatuses.Where(item => item.Status == "Rejected").Sum(item => item.Count),
            TourCount = tours.Count,
            Pois = pois.Select(poi =>
            {
                var vi = poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi") ?? poi.Translations.FirstOrDefault();
                return new EditorMyPoiItemViewModel
                {
                    Id = poi.Id,
                    Name = vi?.Name ?? $"POI #{poi.Id}",
                    Status = poi.Status,
                    AdminNote = poi.AdminNote,
                    CreatedAt = poi.CreatedAt,
                    LastUpdatedAt = poi.Translations.Count == 0 ? null : poi.Translations.Max(item => item.UpdatedAt),
                    TranslationCount = poi.Translations.Count,
                    AudioCount = poi.Translations.Count(item => !string.IsNullOrWhiteSpace(item.AudioUrl)),
                    MenuItemCount = menuCounts.TryGetValue(poi.Id, out var menuCount) ? menuCount : 0,
                    Latitude = poi.Latitude,
                    Longitude = poi.Longitude
                };
            }).ToList(),
            Tours = tours.Select(tour =>
            {
                var vi = tour.Translations.FirstOrDefault(item => item.LanguageCode == "vi") ?? tour.Translations.FirstOrDefault();
                return new EditorMyTourItemViewModel
                {
                    Id = tour.Id,
                    Title = vi?.Title ?? $"Tour #{tour.Id}",
                    Status = tour.Status,
                    CreatedAt = tour.CreatedAt,
                    LoggedAt = loggedAtByTourId.TryGetValue(tour.Id, out var loggedAt) ? loggedAt : null,
                    EstimatedTime = tour.EstimatedTime,
                    PoiCount = tour.TourPois.Count
                };
            }).ToList()
        };

        return View(model);
    }

    private int? GetCurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
    }
}
