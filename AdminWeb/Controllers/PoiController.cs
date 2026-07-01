using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace AdminWeb.Controllers
{
    // Dùng Roles để controller này chạy được cả /Admin/Poi và /Areas/Editor/Poi.
    // VersaSmartScheme trong Program.cs sẽ tự chọn cookie AdminScheme hoặc EditorScheme theo URL.
    [Authorize(Roles = "Admin,Editor")]
    public class PoiController : Controller
    {
        private const int PageSize = 12;
        private const long MaxImageBytes = 5 * 1024 * 1024;
        private const long MaxAudioBytes = 25 * 1024 * 1024;
        private const long MaxVideoBytes = 100 * 1024 * 1024;

        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private static readonly HashSet<string> AllowedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".webm"
        };

        private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".mov", ".m4v"
        };

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment? _environment;

        public PoiController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> Index(string? searchString, int page = 1)
        {
            page = Math.Max(1, page);

            var query = _context.Pois
                .AsNoTracking()
                .Include(p => p.Translations)
                .Where(p =>
                    p.Latitude >= -90 && p.Latitude <= 90 &&
                    p.Longitude >= -180 && p.Longitude <= 180)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();
                query = query.Where(p =>
                    p.Translations.Any(t => t.Name.Contains(keyword)) ||
                    p.Id.ToString() == keyword);
            }

            var totalItems = await query.CountAsync();

            // Nạp dữ liệu bản đồ trực tiếp từ server để trang /Admin/Poi không bị rỗng khi AJAX
            // /Admin/Poi/MapData bị cache, bị chuyển login hoặc trình duyệt còn cookie portal khác.
            var mapPois = await query
                .OrderBy(p => p.Id)
                .ToListAsync();

            ViewBag.PoiMapData = BuildMapData(mapPois);

            var poi = mapPois
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            ViewBag.SearchString = searchString;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

            return View(poi);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new PoiCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PoiCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!IsValidCoordinate(model.Latitude, model.Longitude))
            {
                ModelState.AddModelError(string.Empty, "Tọa độ POI không hợp lệ.");
                return View(model);
            }

            if (model.Radius < 10)
            {
                ModelState.AddModelError(nameof(model.Radius), "Bán kính phải từ 10m trở lên.");
                return View(model);
            }

            var poi = new Poi
            {
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                Radius = model.Radius,
                QrCodeToken = Guid.NewGuid().ToString("N"),
                Status = IsAdminRequest() ? "Approved" : "Pending",
                CreatedAt = DateTime.Now,
                CreatedBy = GetUserId()
            };

            var coverUrl = await SaveUploadedFileAsync(
                model.CoverImage,
                "uploads/poi-covers",
                AllowedImageExtensions,
                MaxImageBytes,
                "ảnh bìa");

            if (coverUrl.Error != null)
            {
                ModelState.AddModelError(nameof(model.CoverImage), coverUrl.Error);
                return View(model);
            }

            poi.CoverImageUrl = coverUrl.Url;

            var translation = new PoiTranslation
            {
                LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? "vi" : model.LanguageCode.Trim().ToLowerInvariant(),
                Name = model.Name.Trim(),
                ShortDescription = model.ShortDescription.Trim(),
                FullDescription = model.FullDescription.Trim(),
                TtsScript = model.FullDescription.Trim(),
                UpdatedAt = DateTime.Now
            };
            poi.Translations.Add(translation);

            var audioUrl = await SaveUploadedFileAsync(
                model.SourceAudio,
                "uploads/audio",
                AllowedAudioExtensions,
                MaxAudioBytes,
                "audio gốc");

            if (audioUrl.Error != null)
            {
                ModelState.AddModelError(nameof(model.SourceAudio), audioUrl.Error);
                return View(model);
            }

            var videoUrl = await SaveUploadedFileAsync(
                model.SourceVideo,
                "uploads/video",
                AllowedVideoExtensions,
                MaxVideoBytes,
                "video gốc");

            if (videoUrl.Error != null)
            {
                ModelState.AddModelError(nameof(model.SourceVideo), videoUrl.Error);
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(audioUrl.Url))
            {
                translation.AudioUrl = audioUrl.Url;
            }

            if (!string.IsNullOrWhiteSpace(videoUrl.Url))
            {
                translation.VideoUrl = videoUrl.Url;
                poi.MediaAssets.Add(new MediaAsset
                {
                    MediaType = "video",
                    MediaUrl = videoUrl.Url,
                    SortOrder = 0
                });
            }

            _context.Pois.Add(poi);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = IsAdminRequest()
                ? "Đã tạo POI và tự động phê duyệt."
                : "Đã lưu POI và gửi sang trạng thái chờ duyệt.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var poi = await _context.Pois
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (poi == null)
            {
                return NotFound();
            }

            return View(poi);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Poi input, IFormFile? coverImage)
        {
            if (id != input.Id)
            {
                return BadRequest();
            }

            var poi = await _context.Pois.FindAsync(id);
            if (poi == null)
            {
                return NotFound();
            }

            if (!IsValidCoordinate(input.Latitude, input.Longitude))
            {
                ModelState.AddModelError(string.Empty, "Tọa độ POI không hợp lệ.");
                return View(poi);
            }

            poi.Latitude = input.Latitude;
            poi.Longitude = input.Longitude;
            poi.Radius = Math.Max(10, input.Radius);

            if (IsAdminRequest())
            {
                poi.Status = NormalizeStatus(input.Status);
            }
            else
            {
                // Editor sửa nội dung thì luôn đưa về Pending để Reviewer/Admin duyệt lại.
                poi.Status = "Pending";
            }

            var coverUrl = await SaveUploadedFileAsync(
                coverImage,
                "uploads/poi-covers",
                AllowedImageExtensions,
                MaxImageBytes,
                "ảnh bìa");

            if (coverUrl.Error != null)
            {
                ModelState.AddModelError(nameof(coverImage), coverUrl.Error);
                return View(poi);
            }

            if (!string.IsNullOrWhiteSpace(coverUrl.Url))
            {
                poi.CoverImageUrl = coverUrl.Url;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã cập nhật thông số POI.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ManageCategories(int id)
        {
            var poi = await _context.Pois
                .Include(p => p.Translations)
                .Include(p => p.PoiCategories)
                    .ThenInclude(pc => pc.Category)
                        .ThenInclude(c => c!.Translations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (poi == null)
            {
                return NotFound();
            }

            var assignedIds = poi.PoiCategories.Select(pc => pc.CategoryId).ToHashSet();
            ViewBag.AvailableCategories = await _context.Categories
                .AsNoTracking()
                .Include(c => c.Translations)
                .Where(c => c.Status == "active" && !assignedIds.Contains(c.Id))
                .OrderBy(c => c.Id)
                .ToListAsync();

            return View(poi);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(int poiId, int categoryId)
        {
            var poiExists = await _context.Pois.AnyAsync(p => p.Id == poiId);
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == categoryId);

            if (!poiExists || !categoryExists)
            {
                return NotFound();
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCategory(int poiId, int categoryId)
        {
            var relation = await _context.PoiCategories
                .FirstOrDefaultAsync(pc => pc.PoiId == poiId && pc.CategoryId == categoryId);

            if (relation != null)
            {
                _context.PoiCategories.Remove(relation);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(ManageCategories), new { id = poiId });
        }

        [HttpGet]
        public async Task<IActionResult> QrCode(int id)
        {
            var poi = await _context.Pois
                .AsNoTracking()
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (poi == null)
            {
                return NotFound();
            }

            var vi = poi.Translations.FirstOrDefault(t => t.LanguageCode == "vi")
                     ?? poi.Translations.FirstOrDefault();

            var payload = BuildQrPayload(poi.Id);
            var model = new PoiQrViewModel
            {
                PoiId = poi.Id,
                PoiName = vi?.Name ?? $"POI #{poi.Id}",
                QrCodeToken = poi.QrCodeToken,
                QrPayload = payload
            };

            return View(model);
        }

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
            TempData["SuccessMessage"] = "Đã tạo lại mã QR.";

            return RedirectToAction(nameof(QrCode), new { id });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var poi = await _context.Pois
                .AsNoTracking()
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (poi == null)
            {
                return NotFound();
            }

            return View(poi);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id, string? confirm)
        {
            var poi = await _context.Pois
                .Include(p => p.Translations)
                .Include(p => p.MediaAssets)
                .Include(p => p.MediaTasks)
                .Include(p => p.PoiCategories)
                .Include(p => p.TourPois)
                .Include(p => p.Discoveries)
                .Include(p => p.PlaybackLogs)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (poi == null)
            {
                return NotFound();
            }

            _context.Pois.Remove(poi);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa POI.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel()
        {
            var pois = await _context.Pois
                .AsNoTracking()
                .Include(p => p.Translations)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Id,Name,Latitude,Longitude,Radius,Status,CreatedAt");

            foreach (var poi in pois)
            {
                var vi = poi.Translations.FirstOrDefault(t => t.LanguageCode == "vi")
                         ?? poi.Translations.FirstOrDefault();

                csv.AppendLine(string.Join(',', new[]
                {
                    EscapeCsv(poi.Id.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(vi?.Name ?? $"POI #{poi.Id}"),
                    EscapeCsv(poi.Latitude.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(poi.Longitude.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(poi.Radius.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(poi.Status),
                    EscapeCsv(poi.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                }));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"pois-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        [HttpGet("/Admin/Poi/MapData")]
        public async Task<IActionResult> MapData()
        {
            var pois = await _context.Pois
                .AsNoTracking()
                .Include(p => p.Translations)
                .Where(p =>
                    p.Latitude >= -90 && p.Latitude <= 90 &&
                    p.Longitude >= -180 && p.Longitude <= 180)
                .OrderBy(p => p.Id)
                .ToListAsync();

            return Json(BuildMapData(pois));
        }

        private static List<object> BuildMapData(IEnumerable<Poi> pois)
        {
            return pois.Select(p =>
            {
                var vi = p.Translations.FirstOrDefault(t => t.LanguageCode == "vi")
                         ?? p.Translations.FirstOrDefault();

                return (object)new
                {
                    id = p.Id,
                    name = vi?.Name ?? $"POI #{p.Id}",
                    shortDescription = vi?.ShortDescription ?? "",
                    latitude = (double)p.Latitude,
                    longitude = (double)p.Longitude,
                    radius = p.Radius,
                    status = string.IsNullOrWhiteSpace(p.Status) ? "Pending" : p.Status,
                    coverImageUrl = p.CoverImageUrl ?? "",
                    createdAt = p.CreatedAt.ToString("dd/MM/yyyy")
                };
            }).ToList();
        }

        private string BuildQrPayload(int poiId)
        {
            var token = _context.Pois
                .AsNoTracking()
                .Where(p => p.Id == poiId)
                .Select(p => p.QrCodeToken)
                .FirstOrDefault();

            return Url.Action(
                    "Details",
                    "Map",
                    new { area = "DuKhach", id = poiId, lang = "auto", token },
                    Request.Scheme)
                ?? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/DuKhach/Map/Details/{poiId}?lang=auto&token={Uri.EscapeDataString(token ?? string.Empty)}";
        }

        [AllowAnonymous]
        [HttpGet("/Admin/Poi/QrImage/{id:int}")]
        public async Task<IActionResult> QrImage(int id, bool download = false)
        {
            var poi = await _context.Pois.FindAsync(id);

            if (poi == null)
            {
                return NotFound();
            }

            var payload = BuildQrPayload(id);

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var bytes = qrCode.GetGraphic(20);
            if (download)
            {
                return File(bytes, "image/png", $"poi-{id}-qr.png");
            }

            return File(bytes, "image/png");
        }

        private async Task<(string? Url, string? Error)> SaveUploadedFileAsync(
            IFormFile? file,
            string relativeFolder,
            HashSet<string> allowedExtensions,
            long maxBytes,
            string label)
        {
            if (file is not { Length: > 0 })
            {
                return (null, null);
            }

            if (file.Length > maxBytes)
            {
                return (null, $"File {label} vượt quá dung lượng cho phép ({maxBytes / 1024 / 1024}MB). ");
            }

            var extension = Path.GetExtension(Path.GetFileName(file.FileName));
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                return (null, $"File {label} không đúng định dạng cho phép.");
            }

            var webRoot = _environment?.WebRootPath;
            var contentRoot = _environment?.ContentRootPath ?? Directory.GetCurrentDirectory();
            webRoot ??= Path.Combine(contentRoot, "wwwroot");

            var normalizedFolder = relativeFolder.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var uploadFolder = Path.Combine(webRoot, normalizedFolder);
            Directory.CreateDirectory(uploadFolder);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadFolder, fileName);

            await using var stream = new FileStream(fullPath, FileMode.CreateNew);
            await file.CopyToAsync(stream);

            return ($"/{relativeFolder.Trim('/')}/{fileName}".Replace("\\", "/"), null);
        }

        private bool IsAdminRequest()
        {
            return User.IsInRole("Admin") && !IsEditorAreaRequest();
        }

        private bool IsEditorAreaRequest()
        {
            var area = RouteData.Values["area"]?.ToString();
            return string.Equals(area, "Editor", StringComparison.OrdinalIgnoreCase)
                   || Request.Path.StartsWithSegments("/Areas/Editor")
                   || Request.Path.StartsWithSegments("/Editor");
        }

        private int? GetUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : null;
        }

        private static bool IsValidCoordinate(decimal latitude, decimal longitude)
        {
            return latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180;
        }

        private static string NormalizeStatus(string? status)
        {
            return status switch
            {
                "Approved" => "Approved",
                "Rejected" => "Rejected",
                _ => "Pending"
            };
        }

        private static string EscapeCsv(string? value)
        {
            value ??= string.Empty;
            return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
