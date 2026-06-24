using AdminWeb.Areas.Reviewer.Models;
using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Areas.Reviewer.Controllers;

[Area("Reviewer")]
[Authorize(Policy = "ReviewerAreaPolicy")]
public sealed class DashboardController : Controller
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var activeLanguageCount = await _context.SupportedLanguages
            .AsNoTracking()
            .CountAsync(language => language.IsActive, cancellationToken);
        activeLanguageCount = Math.Max(activeLanguageCount, 1);

        var pendingPois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Where(poi => poi.Status == "Pending")
            .OrderByDescending(poi => poi.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var model = new ReviewerDashboardViewModel
        {
            PendingPois = pendingPois.Count,
            ApprovedPois = await _context.Pois.CountAsync(poi => poi.Status == "Approved", cancellationToken),
            RejectedPois = await _context.Pois.CountAsync(poi => poi.Status == "Rejected", cancellationToken),
            PendingOlderThan3Days = pendingPois.Count(poi => (now - poi.CreatedAt.ToUniversalTime()).TotalDays >= 3),
            PendingMissingTranslation = pendingPois.Count(poi => poi.Translations.Select(item => item.LanguageCode).Distinct().Count() < activeLanguageCount),
            PendingMissingAudio = pendingPois.Count(poi => poi.Translations.Any(translation => string.IsNullOrWhiteSpace(translation.AudioUrl))),
            AveragePendingAgeHours = pendingPois.Count == 0 ? 0 : Convert.ToInt32(pendingPois.Average(poi => Math.Max(0, (now - poi.CreatedAt.ToUniversalTime()).TotalHours))),
            RecentPendingPois = pendingPois.Take(8).ToList()
        };

        return View(model);
    }
}
