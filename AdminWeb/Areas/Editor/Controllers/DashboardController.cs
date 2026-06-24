using AdminWeb.Areas.Editor.Models;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Areas.Editor.Controllers;

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
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

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Include(poi => poi.MediaAssets)
            .Include(poi => poi.PoiCategories)
            .ToListAsync(cancellationToken);

        var qualityScores = pois.Select(poi => CalculateQuickQualityScore(poi, activeLanguageCount)).ToList();

        var model = new EditorDashboardViewModel
        {
            TotalPois = pois.Count,
            DraftPois = pois.Count(poi => poi.Status == "Draft"),
            PendingPois = pois.Count(poi => poi.Status == "Pending"),
            ApprovedPois = pois.Count(poi => poi.Status == "Approved"),
            RejectedPois = pois.Count(poi => poi.Status == "Rejected"),
            TotalTours = await _context.Tours.CountAsync(cancellationToken),
            TotalCategories = await _context.Categories.CountAsync(cancellationToken),
            MissingTranslationPois = pois.Count(poi => poi.Translations.Select(item => item.LanguageCode).Distinct().Count() < activeLanguageCount),
            MissingAudioPois = pois.Count(poi => poi.Translations.Any(translation => string.IsNullOrWhiteSpace(translation.AudioUrl))),
            PendingMediaTasks = await _context.MediaTasks.CountAsync(
                task => task.Status == MediaTaskStatus.Pending || task.Status == MediaTaskStatus.Processing,
                cancellationToken),
            FailedMediaTasks = await _context.MediaTasks.CountAsync(
                task => task.Status == MediaTaskStatus.Failed || task.Status == MediaTaskStatus.CompletedWithErrors,
                cancellationToken),
            AverageQualityScore = qualityScores.Count == 0 ? 100 : Convert.ToInt32(qualityScores.Average()),
            RecentPois = await _context.Pois
                .AsNoTracking()
                .Include(poi => poi.Translations)
                .OrderByDescending(poi => poi.CreatedAt)
                .Take(6)
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }

    private static int CalculateQuickQualityScore(Poi poi, int activeLanguageCount)
    {
        var vi = poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi") ?? poi.Translations.FirstOrDefault();
        var score = 100;
        var missingLanguageCount = Math.Max(0, activeLanguageCount - poi.Translations.Select(item => item.LanguageCode).Distinct().Count());
        var missingAudioCount = poi.Translations.Count(item => string.IsNullOrWhiteSpace(item.AudioUrl));

        score -= missingLanguageCount * 8;
        score -= missingAudioCount * 5;
        score -= string.IsNullOrWhiteSpace(vi?.ShortDescription) ? 10 : 0;
        score -= string.IsNullOrWhiteSpace(vi?.FullDescription) ? 15 : 0;
        score -= !string.IsNullOrWhiteSpace(poi.CoverImageUrl) || poi.MediaAssets.Any(asset => asset.MediaType == "image") ? 0 : 12;
        score -= poi.PoiCategories.Count > 0 ? 0 : 8;

        return Math.Clamp(score, 0, 100);
    }
}
