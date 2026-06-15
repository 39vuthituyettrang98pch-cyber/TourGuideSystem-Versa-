using System.Text.Json;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Services;

public sealed class SyncPackageService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public SyncPackageService(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<string> GenerateAsync(
        SyncVersion version,
        CancellationToken cancellationToken = default)
    {
        var languages = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(language => language.IsActive)
            .OrderBy(language => language.LanguageCode)
            .Select(language => new
            {
                language.LanguageCode,
                language.LanguageName
            })
            .ToListAsync(cancellationToken);

        var categories = await _context.Categories
            .AsNoTracking()
            .Include(category => category.Translations)
            .Where(category => category.Status == "active")
            .OrderBy(category => category.Id)
            .Select(category => new
            {
                category.Id,
                category.IconUrl,
                translations = category.Translations
                    .OrderBy(translation => translation.LanguageCode)
                    .Select(translation => new
                    {
                        translation.LanguageCode,
                        translation.Name
                    })
            })
            .ToListAsync(cancellationToken);

        var pois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Include(poi => poi.MediaAssets)
            .Include(poi => poi.PoiCategories)
            .Include(poi => poi.Beacons)
            .Where(poi => poi.Status == "Approved")
            .OrderBy(poi => poi.Id)
            .Select(poi => new
            {
                poi.Id,
                poi.Latitude,
                poi.Longitude,
                poi.Radius,
                poi.QrCodeToken,
                poi.CoverImageUrl,
                translations = poi.Translations
                    .OrderBy(translation => translation.LanguageCode)
                    .Select(translation => new
                    {
                        translation.LanguageCode,
                        translation.Name,
                        translation.ShortDescription,
                        translation.FullDescription,
                        translation.AudioUrl,
                        translation.VideoUrl,
                        translation.AudioDuration
                    }),
                categoryIds = poi.PoiCategories.Select(item => item.CategoryId),
                media = poi.MediaAssets
                    .OrderBy(item => item.SortOrder)
                    .Select(item => new
                    {
                        item.MediaType,
                        item.MediaUrl,
                        item.SortOrder
                    }),
                beacons = poi.Beacons.Select(beacon => new
                {
                    beacon.Uuid,
                    beacon.Major,
                    beacon.Minor,
                    beacon.MacAddress
                })
            })
            .ToListAsync(cancellationToken);

        var tours = await _context.Tours
            .AsNoTracking()
            .Include(tour => tour.Translations)
            .Include(tour => tour.TourPois)
            .Where(tour => tour.Status == "active")
            .OrderBy(tour => tour.Id)
            .Select(tour => new
            {
                tour.Id,
                tour.EstimatedTime,
                translations = tour.Translations
                    .OrderBy(translation => translation.LanguageCode)
                    .Select(translation => new
                    {
                        translation.LanguageCode,
                        translation.Title,
                        translation.Description
                    }),
                poiIds = tour.TourPois
                    .Where(item => item.Poi!.Status == "Approved")
                    .OrderBy(item => item.SequenceOrder)
                    .Select(item => item.PoiId)
            })
            .ToListAsync(cancellationToken);

        var document = new
        {
            version = version.VersionNumber,
            description = version.Description,
            generatedAt = DateTimeOffset.UtcNow,
            languages,
            categories,
            pois,
            tours
        };

        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(webRoot, "sync");
        Directory.CreateDirectory(directory);

        var path = GetPackagePath(version, directory);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            document,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            },
            cancellationToken);

        return path;
    }

    public string? FindPackagePath(SyncVersion version)
    {
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var path = GetPackagePath(version, Path.Combine(webRoot, "sync"));
        return File.Exists(path) ? path : null;
    }

    public void DeletePackage(SyncVersion version)
    {
        var path = FindPackagePath(version);
        if (path != null)
            File.Delete(path);
    }

    private static string GetPackagePath(SyncVersion version, string directory)
    {
        var safeVersion = string.Concat(version.VersionNumber.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return Path.Combine(directory, $"{version.Id}-{safeVersion}.json");
    }
}
