using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services.MediaProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class PoiController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _hostEnvironment;

    public PoiController(
        AppDbContext context,
        IWebHostEnvironment hostEnvironment)
    {
        _context = context;
        _hostEnvironment = hostEnvironment;
    }

    // GET: /Poi/
    public async Task<IActionResult> Index(string? searchString, int page = 1)
    {
        const int pageSize = 10;

        var query = _context.Pois
            .Include(p => p.Translations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            var lowerSearch = searchString.Trim().ToLower();

            query = query.Where(p =>
                p.Translations.Any(t =>
                    t.Name.ToLower().Contains(lowerSearch)));
        }

        query = query.OrderByDescending(p => p.CreatedAt);

        var totalItems = await query.CountAsync();

        var pois = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.SearchString = searchString;

        return View(pois);
    }

    // API cho bản đồ tổng quan Admin
    // GET: /Poi/MapData
    [HttpGet]
    public async Task<IActionResult> MapData()
    {
        var pois = await _context.Pois
            .Include(p => p.Translations)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var data = pois.Select(poi =>
        {
            var vi = poi.Translations.FirstOrDefault(t => t.LanguageCode == "vi");
            var first = poi.Translations.FirstOrDefault();

            return new
            {
                id = poi.Id,
                name = vi?.Name ?? first?.Name ?? $"POI #{poi.Id}",
                shortDescription = vi?.ShortDescription ?? first?.ShortDescription,
                latitude = poi.Latitude,
                longitude = poi.Longitude,
                radius = poi.Radius,
                status = poi.Status,
                coverImageUrl = poi.CoverImageUrl,
                createdAt = poi.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            };
        });

        return Json(data);
    }

    // GET: /Poi/Create
    public IActionResult Create()
    {
        return View(new ViewModels.PoiCreateViewModel
        {
            Latitude = 10.762622m,
            Longitude = 106.660172m,
            Radius = 50,
            LanguageCode = "vi"
        });
    }

    // POST: /Poi/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(524_288_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    public async Task<IActionResult> Create(ViewModels.PoiCreateViewModel model)
    {
        if (model.SourceAudio is { Length: > 0 } &&
            !model.SourceAudio.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.SourceAudio), "File tải lên phải là file audio.");
        }

        if (model.SourceVideo is { Length: > 0 } &&
            !model.SourceVideo.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.SourceVideo), "File tải lên phải là file video.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var cancellationToken = HttpContext.RequestAborted;

        var coverImage = await SaveUploadedFileAsync(
            model.CoverImage,
            "poi_covers",
            cancellationToken);

        var sourceAudio = await SaveUploadedFileAsync(
            model.SourceAudio,
            "poi_source_media",
            cancellationToken);

        var sourceVideo = await SaveUploadedFileAsync(
            model.SourceVideo,
            "poi_source_media",
            cancellationToken);

        var originalTranslation = new PoiTranslation
        {
            LanguageCode = "vi",
            Name = model.Name,
            ShortDescription = model.ShortDescription,
            FullDescription = model.FullDescription,
            TtsScript = model.FullDescription,
            AudioUrl = sourceAudio.WebUrl,
            VideoUrl = sourceVideo.WebUrl,
            UpdatedAt = DateTime.Now
        };

        var mediaTask = new MediaTask
        {
            TaskType = sourceVideo.PhysicalPath != null
                ? MediaTaskType.VideoDubbing
                : MediaTaskType.TextToAudio,
            Status = MediaTaskStatus.Pending,
            ProgressPercentage = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var poi = new Poi
        {
            Latitude = model.Latitude,
            Longitude = model.Longitude,
            Radius = model.Radius,
            QrCodeToken = Guid.NewGuid().ToString("N"),
            Status = User.IsInRole("Admin") ? "Approved" : "Pending",
            AdminNote = null,
            CoverImageUrl = coverImage.WebUrl,
            CreatedAt = DateTime.Now,
            Translations = new List<PoiTranslation>
            {
                originalTranslation
            },
            MediaTasks = new List<MediaTask>
            {
                mediaTask
            }
        };

        _context.Pois.Add(poi);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] =
            "Đã lưu POI. Hệ thống đang chạy ngầm AI dịch thuật và lồng tiếng đa ngôn ngữ.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Poi/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var poi = await _context.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (poi == null)
        {
            return NotFound();
        }

        return View(poi);
    }

    // POST: /Poi/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Poi poi, IFormFile? coverImage)
    {
        if (id != poi.Id)
        {
            return NotFound();
        }

        if (coverImage is { Length: > 0 } &&
            !coverImage.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(coverImage), "File ảnh bìa không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(poi);
        }

        var existing = await _context.Pois.FindAsync(id);

        if (existing == null)
        {
            return NotFound();
        }

        existing.Latitude = poi.Latitude;
        existing.Longitude = poi.Longitude;
        existing.Radius = poi.Radius;

        if (coverImage is { Length: > 0 })
        {
            var savedCover = await SaveUploadedFileAsync(
                coverImage,
                "poi_covers",
                HttpContext.RequestAborted);

            existing.CoverImageUrl = savedCover.WebUrl;
        }

        if (User.IsInRole("Admin"))
        {
            existing.Status = poi.Status;
        }
        else
        {
            existing.Status = "Pending";
            existing.AdminNote = null;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã cập nhật thông số POI.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Poi/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var poi = await _context.Pois
            .FirstOrDefaultAsync(m => m.Id == id);

        if (poi == null)
        {
            return NotFound();
        }

        return View(poi);
    }

    // POST: /Poi/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var poi = await _context.Pois.FindAsync(id);

        if (poi != null)
        {
            _context.Pois.Remove(poi);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa POI.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Poi/QrCode/5
    public async Task<IActionResult> QrCode(int id)
    {
        var poi = await _context.Pois
            .Include(item => item.Translations)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (poi == null)
        {
            return NotFound();
        }

        await EnsureQrTokenAsync(poi);

        var name = poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")?.Name
            ?? poi.Translations.FirstOrDefault()?.Name
            ?? $"POI #{poi.Id}";

        return View(new ViewModels.PoiQrViewModel
        {
            PoiId = poi.Id,
            PoiName = name,
            QrCodeToken = poi.QrCodeToken,
            QrPayload = BuildQrPayload(poi.QrCodeToken)
        });
    }

    // GET: /Poi/QrImage/5
    public async Task<IActionResult> QrImage(int id, bool download = false)
    {
        var poi = await _context.Pois
            .FirstOrDefaultAsync(item => item.Id == id);

        if (poi == null)
        {
            return NotFound();
        }

        await EnsureQrTokenAsync(poi);

        using var generator = new QRCodeGenerator();

        using var qrData = generator.CreateQrCode(
            BuildQrPayload(poi.QrCodeToken),
            QRCodeGenerator.ECCLevel.Q);

        using var qrCode = new PngByteQRCode(qrData);

        var image = qrCode.GetGraphic(20);

        return download
            ? File(image, "image/png", $"POI-{poi.Id}-QR.png")
            : File(image, "image/png");
    }

    // POST: /Poi/RegenerateQr/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegenerateQr(int id)
    {
        var poi = await _context.Pois.FindAsync(id);

        if (poi == null)
        {
            return NotFound();
        }

        poi.QrCodeToken = Guid.NewGuid().ToString("N");

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã tạo lại mã QR. Mã QR cũ sẽ không còn hiệu lực.";

        return RedirectToAction(nameof(QrCode), new { id });
    }

    // GET: /Poi/ExportExcel
    public async Task<IActionResult> ExportExcel()
    {
        var pois = await _context.Pois
            .Include(p => p.Translations)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var builder = new StringBuilder();

        builder.Append('\uFEFF');
        builder.AppendLine("ID,Ten_Dia_Diem,Vi_Do,Kinh_Do,Ban_Kinh,Trang_Thai,Ngay_Tao");

        foreach (var poi in pois)
        {
            var name = poi.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
                ?? poi.Translations.FirstOrDefault()?.Name
                ?? $"POI #{poi.Id}";

            name = $"\"{name.Replace("\"", "\"\"")}\"";

            builder.AppendLine(
                $"{poi.Id},{name},{poi.Latitude},{poi.Longitude},{poi.Radius},{poi.Status},{poi.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        return File(
            Encoding.UTF8.GetBytes(builder.ToString()),
            "text/csv",
            $"DanhSach_POI_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    // ==========================================
    // QUẢN LÝ DANH MỤC CHO POI
    // ==========================================

    // GET: /Poi/ManageCategories/5
    public async Task<IActionResult> ManageCategories(int id)
    {
        var poi = await _context.Pois
            .Include(p => p.PoiCategories)
                .ThenInclude(pc => pc.Category)
                    .ThenInclude(c => c!.Translations)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poi == null)
        {
            return NotFound();
        }

        var existingCategoryIds = poi.PoiCategories?
            .Select(pc => pc.CategoryId)
            .ToList() ?? new List<int>();

        ViewBag.AvailableCategories = await _context.Categories
            .Include(c => c.Translations)
            .Where(c => !existingCategoryIds.Contains(c.Id))
            .ToListAsync();

        return View(poi);
    }

    // POST: /Poi/AddCategory
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(int poiId, int categoryId)
    {
        var exists = await _context.PoiCategories
            .AnyAsync(pc => pc.PoiId == poiId && pc.CategoryId == categoryId);

        if (!exists)
        {
            _context.PoiCategories.Add(new PoiCategory
            {
                PoiId = poiId,
                CategoryId = categoryId
            });

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ManageCategories), new { id = poiId });
    }

    // POST: /Poi/RemoveCategory
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCategory(int poiId, int categoryId)
    {
        var poiCategory = await _context.PoiCategories
            .FirstOrDefaultAsync(pc => pc.PoiId == poiId && pc.CategoryId == categoryId);

        if (poiCategory != null)
        {
            _context.PoiCategories.Remove(poiCategory);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ManageCategories), new { id = poiId });
    }

    private async Task<(string? WebUrl, string? PhysicalPath)> SaveUploadedFileAsync(
        IFormFile? file,
        string subfolder,
        CancellationToken cancellationToken)
    {
        if (file is not { Length: > 0 })
        {
            return (null, null);
        }

        var webRootPath = _hostEnvironment.WebRootPath
            ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");

        var uploadsFolder = Path.Combine(webRootPath, "uploads", subfolder);

        Directory.CreateDirectory(uploadsFolder);

        var extension = Path.GetExtension(Path.GetFileName(file.FileName));

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

        await using var fileStream = new FileStream(physicalPath, FileMode.CreateNew);

        await file.CopyToAsync(fileStream, cancellationToken);

        return ($"/uploads/{subfolder}/{uniqueFileName}", physicalPath);
    }

    private async Task EnsureQrTokenAsync(Poi poi)
    {
        if (!string.IsNullOrWhiteSpace(poi.QrCodeToken))
        {
            return;
        }

        poi.QrCodeToken = Guid.NewGuid().ToString("N");

        await _context.SaveChangesAsync();
    }

    private static string BuildQrPayload(string token)
    {
        return $"versa://poi/{token}";
    }
}