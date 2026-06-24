using AdminWeb.Areas.DuKhach.Models;
using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
[Authorize(Policy = "TouristAreaPolicy")]
public sealed class TourController : Controller
{
    private const string FallbackLanguageCode = "vi";

    private readonly AppDbContext _context;

    public TourController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!await HasActivePremiumAsync(cancellationToken))
        {
            TempData["DuKhachErrorMessage"] = "Tour du lịch là tính năng Premium. Vui lòng mua gói Premium để mở khóa.";
            return RedirectToAction("Plans", "Payments", new { area = "DuKhach" });
        }

        var selectedLanguageCode = GetSelectedLanguageCode();
        ViewBag.SelectedLanguageCode = selectedLanguageCode;

        var tours = await _context.Tours
            .AsNoTracking()
            .Include(tour => tour.Translations)
            .Include(tour => tour.TourPois)
            .Where(tour => tour.Status == "active" || tour.Status == "Active")
            .ToListAsync(cancellationToken);

        var model = tours.Select(tour =>
        {
            var translation = tour.Translations.FirstOrDefault(item => item.LanguageCode == selectedLanguageCode)
                ?? tour.Translations.FirstOrDefault(item => item.LanguageCode == "vi")
                ?? tour.Translations.FirstOrDefault(item => item.LanguageCode == "en")
                ?? tour.Translations.FirstOrDefault();

            return new DuKhachTourListViewModel
            {
                Id = tour.Id,
                Name = translation?.Title ?? $"Tour #{tour.Id}",
                Description = translation?.Description ?? "",
                EstimatedTime = tour.EstimatedTime,
                PoiCount = tour.TourPois.Count
            };
        }).ToList();

        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (!await HasActivePremiumAsync(cancellationToken))
        {
            TempData["DuKhachErrorMessage"] = "Chi tiết tour là tính năng Premium. Vui lòng mua gói Premium để mở khóa.";
            return RedirectToAction("Plans", "Payments", new { area = "DuKhach" });
        }

        var selectedLanguageCode = GetSelectedLanguageCode();
        ViewBag.SelectedLanguageCode = selectedLanguageCode;

        var tour = await _context.Tours
            .AsNoTracking()
            .Include(tour => tour.Translations)
            .Include(tour => tour.TourPois)
                .ThenInclude(tourPoi => tourPoi.Poi)
                    .ThenInclude(poi => poi!.Translations)
            .FirstOrDefaultAsync(tour => tour.Id == id, cancellationToken);

        if (tour == null)
        {
            return NotFound();
        }

        var selectedTourTranslation = tour.Translations.FirstOrDefault(item => item.LanguageCode == selectedLanguageCode)
            ?? tour.Translations.FirstOrDefault(item => item.LanguageCode == "vi")
            ?? tour.Translations.FirstOrDefault(item => item.LanguageCode == "en")
            ?? tour.Translations.FirstOrDefault();

        var model = new DuKhachTourDetailViewModel
        {
            Id = tour.Id,
            Name = selectedTourTranslation?.Title ?? $"Tour #{tour.Id}",
            Description = selectedTourTranslation?.Description ?? "",
            EstimatedTime = tour.EstimatedTime,
            TravelMode = "driving",
            Pois = tour.TourPois.OrderBy(item => item.SequenceOrder).Select(tourPoi =>
            {
                var poi = tourPoi.Poi;
                var selectedPoiTranslation = poi?.Translations?.FirstOrDefault(item => item.LanguageCode == selectedLanguageCode)
                    ?? poi?.Translations?.FirstOrDefault(item => item.LanguageCode == "vi")
                    ?? poi?.Translations?.FirstOrDefault(item => item.LanguageCode == "en")
                    ?? poi?.Translations?.FirstOrDefault();

                return new DuKhachPoiMapItemViewModel
                {
                    Id = poi?.Id ?? 0,
                    Name = selectedPoiTranslation?.Name ?? $"POI #{poi?.Id}",
                    ShortDescription = selectedPoiTranslation?.ShortDescription ?? "",
                    FullDescription = selectedPoiTranslation?.FullDescription ?? "",
                    NarrationText = selectedPoiTranslation?.TtsScript ?? selectedPoiTranslation?.FullDescription ?? "",
                    AudioUrl = selectedPoiTranslation?.AudioUrl,
                    VideoUrl = selectedPoiTranslation?.VideoUrl,
                    LanguageCode = selectedPoiTranslation?.LanguageCode ?? selectedLanguageCode,
                    Latitude = poi != null ? (double)poi.Latitude : 0,
                    Longitude = poi != null ? (double)poi.Longitude : 0,
                    Radius = poi?.Radius ?? 50
                };
            }).ToList()
        };

        return View(model);
    }

    private async Task<bool> HasActivePremiumAsync(CancellationToken cancellationToken)
    {
        var touristIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(touristIdText, out var touristId))
            return false;

        var now = DateTime.UtcNow;
        return await _context.TouristSubscriptions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .AnyAsync(item =>
                item.TouristId == touristId &&
                item.Status == "Active" &&
                item.ExpiresAt > now &&
                item.PaymentPlan != null &&
                (item.PaymentPlan.PlanCode == "USER_PREMIUM" || item.PaymentPlan.Audience == "Tourist" || item.PaymentPlan.Audience == "Both"),
                cancellationToken);
    }

    private string GetSelectedLanguageCode()
    {
        var raw = Request.Query["lang"].FirstOrDefault()
            ?? Request.Cookies["versa.dukhach.lang"]
            ?? "vi";

        var languageCode = NormalizeLanguageCode(raw);
        Response.Cookies.Append("versa.dukhach.lang", languageCode, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });

        return languageCode;
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        var normalized = (languageCode ?? FallbackLanguageCode).Trim().ToLowerInvariant();
        return normalized.Length is >= 2 and <= 10 &&
            normalized.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
                ? normalized
                : FallbackLanguageCode;
    }
}
