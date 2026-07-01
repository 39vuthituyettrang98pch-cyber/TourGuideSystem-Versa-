using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class MediaController : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const long MaxVideoBytes = 100 * 1024 * 1024;

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov", ".m4v"
    };

    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _hostEnvironment;

    public MediaController(AppDbContext context, IWebHostEnvironment hostEnvironment)
    {
        _context = context;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? poiId)
    {
        if (poiId == null) return NotFound("Thiếu ID của Điểm tham quan");

        var poi = await _context.Pois
            .Include(p => p.Translations)
            .Include(p => p.MediaAssets)
            .FirstOrDefaultAsync(p => p.Id == poiId);

        if (poi == null) return NotFound("Không tìm thấy Điểm tham quan");

        return View(poi);
    }

    [HttpGet]
    public async Task<IActionResult> Library(int? poiId, int page = 1)
    {
        int pageSize = 12;
        var query = _context.MediaAssets
            .Include(m => m.Poi)
            .ThenInclude(p => p!.Translations)
            .AsQueryable();

        if (poiId.HasValue)
        {
            query = query.Where(m => m.PoiId == poiId.Value);
        }

        query = query.OrderByDescending(m => m.Id);

        var totalItems = await query.CountAsync();
        var allMedia = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Pois = await _context.Pois.Include(p => p.Translations).ToListAsync();
        ViewBag.SelectedPoiId = poiId;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(allMedia);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadMedia(int? poiId, IFormFile? mediaFile, int sortOrder = 0)
    {
        if (poiId == null || !await _context.Pois.AnyAsync(item => item.Id == poiId.Value))
            return NotFound();

        if (mediaFile is not { Length: > 0 })
        {
            TempData["ErrorMessage"] = "Vui lòng chọn file media.";
            return RedirectToAction(nameof(Library), new { poiId });
        }

        var extension = Path.GetExtension(Path.GetFileName(mediaFile.FileName));
        var isVideo = mediaFile.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                      && AllowedVideoExtensions.Contains(extension);
        var isImage = mediaFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                      && AllowedImageExtensions.Contains(extension);

        if (!isVideo && !isImage)
        {
            TempData["ErrorMessage"] = "Chỉ hỗ trợ ảnh .jpg/.jpeg/.png/.webp hoặc video .mp4/.webm/.mov/.m4v.";
            return RedirectToAction(nameof(Library), new { poiId });
        }

        var maxBytes = isVideo ? MaxVideoBytes : MaxImageBytes;
        if (mediaFile.Length > maxBytes)
        {
            TempData["ErrorMessage"] = isVideo
                ? "Video vượt quá 100MB."
                : "Ảnh vượt quá 5MB.";
            return RedirectToAction(nameof(Library), new { poiId });
        }

        var webRoot = _hostEnvironment.WebRootPath
            ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        var uploadsFolder = Path.Combine(webRoot, "uploads", "media");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        await using (var fileStream = new FileStream(filePath, FileMode.CreateNew))
        {
            await mediaFile.CopyToAsync(fileStream);
        }

        _context.MediaAssets.Add(new MediaAsset
        {
            PoiId = poiId.Value,
            MediaType = isVideo ? "video" : "image",
            MediaUrl = "/uploads/media/" + uniqueFileName,
            SortOrder = sortOrder
        });
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã tải media lên.";

        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer) && referer.Contains("Library"))
        {
            return RedirectToAction(nameof(Library), new { poiId = poiId });
        }
        return RedirectToAction(nameof(Index), new { poiId = poiId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteMedia(int id)
    {
        var media = await _context.MediaAssets.FindAsync(id);
        if (media != null)
        {
            var poiId = media.PoiId;
            
            var webRoot = Path.GetFullPath(_hostEnvironment.WebRootPath
                ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot"));
            var filePath = Path.GetFullPath(Path.Combine(webRoot, media.MediaUrl.TrimStart('/')));
            if (filePath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase) &&
                System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _context.MediaAssets.Remove(media);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa media.";
            
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("Library"))
            {
                return RedirectToAction(nameof(Library));
            }
            return RedirectToAction(nameof(Index), new { poiId = poiId });
        }
        return RedirectToAction("Index", "Poi");
    }
}
