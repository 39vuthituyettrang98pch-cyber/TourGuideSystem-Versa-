using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using AdminWeb.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
public sealed class PoiController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly IGeminiService _geminiService;

    public PoiController(
        AppDbContext context,
        IWebHostEnvironment hostEnvironment,
        IGeminiService geminiService)
    {
        _context = context;
        _hostEnvironment = hostEnvironment;
        _geminiService = geminiService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var pois = await _context.Pois
            .Include(poi => poi.Translations)
            .Include(poi => poi.PlaybackLogs)
            .Where(poi => poi.OwnerProfileId == owner.Id)
            .OrderBy(poi => poi.Id)
            .ToListAsync(cancellationToken);

        await NormalizeCompletedRequestsAsync(owner.Id, cancellationToken);

        ViewBag.Owner = owner;
        ViewBag.PendingRequestCount = await _context.PoiOwnerRequests
            .CountAsync(item => item.OwnerProfileId == owner.Id && item.Status == "Pending", cancellationToken);

        return View(pois);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var poi = await _context.Pois
            .Include(item => item.Translations)
            .Include(item => item.MediaAssets)
            .Include(item => item.PlaybackLogs)
            .FirstOrDefaultAsync(item => item.Id == id && item.OwnerProfileId == owner.Id, cancellationToken);

        if (poi == null)
            return NotFound();

        ViewBag.ReviewCount = await _context.PoiReviews.CountAsync(review => review.PoiId == id, cancellationToken);
        ViewBag.AverageRating = await _context.PoiReviews.Where(review => review.PoiId == id).AverageAsync(review => (double?)review.Rating, cancellationToken) ?? 0;
        ViewBag.MenuItemCount = await _context.OwnerMenuItems.CountAsync(item => item.PoiId == id && item.OwnerProfileId == owner.Id, cancellationToken);
        return View(poi);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        await PrepareCreateViewAsync(owner, cancellationToken);
        ViewBag.Latitude = "10.762622";
        ViewBag.Longitude = "106.660172";
        ViewBag.Radius = 50;
        ViewData["Title"] = "Gửi POI mới";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string name,
        string? shortDescription,
        string? fullDescription,
        string? latitude,
        string? longitude,
        int radius,
        string? coverImageUrl,
        IFormFile? coverImage,
        string? note,
        CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        name = (name ?? string.Empty).Trim();
        shortDescription = shortDescription?.Trim();
        fullDescription = fullDescription?.Trim();
        coverImageUrl = coverImageUrl?.Trim();
        note = note?.Trim();

        ViewBag.Name = name;
        ViewBag.ShortDescription = shortDescription;
        ViewBag.FullDescription = fullDescription;
        ViewBag.Latitude = latitude;
        ViewBag.Longitude = longitude;
        ViewBag.Radius = radius;
        ViewBag.CoverImageUrl = coverImageUrl;
        ViewBag.Note = note;
        await PrepareCreateViewAsync(owner, cancellationToken);

        if (coverImage is { Length: > 0 } &&
            !coverImage.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(coverImage), "File ảnh bìa không hợp lệ. Vui lòng chọn file hình ảnh.");
        }

        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(string.Empty, "Vui lòng nhập tên POI/gian hàng.");

        if (!TryParseCoordinate(latitude, out var parsedLatitude) || parsedLatitude < -90 || parsedLatitude > 90)
            ModelState.AddModelError(string.Empty, "Latitude phải nằm trong khoảng -90 đến 90.");

        if (!TryParseCoordinate(longitude, out var parsedLongitude) || parsedLongitude < -180 || parsedLongitude > 180)
            ModelState.AddModelError(string.Empty, "Longitude phải nằm trong khoảng -180 đến 180.");

        if (radius < 10 || radius > 5000)
            ModelState.AddModelError(string.Empty, "Bán kính geofence phải từ 10 đến 5.000 mét.");

        if (!ModelState.IsValid)
            return View();

        if (coverImage is { Length: > 0 })
        {
            var savedCover = await SaveUploadedFileAsync(coverImage, "poi_covers", cancellationToken);
            coverImageUrl = savedCover.WebUrl;
        }

        var poi = new Poi
        {
            Latitude = parsedLatitude,
            Longitude = parsedLongitude,
            Radius = radius,
            QrCodeToken = Guid.NewGuid().ToString("N"),
            Status = "Pending",
            AdminNote = "POI do chủ gian hàng gửi, chờ admin/reviewer duyệt.",
            OwnerProfileId = owner.Id,
            CoverImageUrl = coverImageUrl,
            CreatedAt = DateTime.Now
        };

        poi.Translations.Add(new PoiTranslation
        {
            LanguageCode = "vi",
            Name = name,
            ShortDescription = shortDescription,
            FullDescription = fullDescription,
            TtsScript = fullDescription ?? shortDescription ?? name,
            UpdatedAt = DateTime.Now
        });

        _context.Pois.Add(poi);
        await _context.SaveChangesAsync(cancellationToken);

        _context.PoiOwnerRequests.Add(new PoiOwnerRequest
        {
            OwnerProfileId = owner.Id,
            PoiId = poi.Id,
            RequestType = "Create",
            Status = "Pending",
            Note = string.IsNullOrWhiteSpace(note) ? "Chủ gian hàng gửi POI mới." : note,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã gửi POI mới. POI sẽ hiển thị công khai sau khi admin/reviewer duyệt.";
        return RedirectToAction(nameof(Index), "Poi", new { area = "Owner" });
    }


    public sealed class OwnerOptimizeRequest
    {
        public string RawText { get; set; } = string.Empty;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OptimizeContent([FromBody] OwnerOptimizeRequest request, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return Unauthorized(new { success = false, message = "Bạn cần đăng nhập Owner." });

        if (!await HasPremiumAiAsync(owner.Id, cancellationToken))
        {
            return StatusCode(403, new
            {
                success = false,
                message = "Tính năng AI tối ưu nội dung chỉ dành cho gói 199.000đ / Premium."
            });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.RawText))
            return BadRequest(new { success = false, message = "Vui lòng nhập nội dung thô trước khi tối ưu." });

        try
        {
            var result = await _geminiService.OptimizePoiContentAsync(request.RawText.Trim(), cancellationToken);
            var parts = result.Split(new[] { "|||" }, StringSplitOptions.None);
            var shortText = parts.Length > 0 ? parts[0] : string.Empty;
            var fullText = parts.Length > 1 ? parts[1] : string.Empty;

            return Ok(new
            {
                success = true,
                shortDescription = shortText,
                fullDescription = fullText
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Claim(CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        ViewBag.Pois = await _context.Pois
            .Include(poi => poi.Translations)
            .Where(poi => poi.OwnerProfileId == null && poi.Status == "Approved")
            .OrderBy(poi => poi.Id)
            .ToListAsync(cancellationToken);

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int poiId, string? note, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var poiExists = await _context.Pois.AnyAsync(poi => poi.Id == poiId && poi.OwnerProfileId == null, cancellationToken);
        if (!poiExists)
        {
            TempData["ErrorMessage"] = "POI không tồn tại hoặc đã có chủ gian hàng quản lý.";
            return RedirectToAction(nameof(Claim), "Poi", new { area = "Owner" });
        }

        var pendingExists = await _context.PoiOwnerRequests.AnyAsync(item =>
            item.OwnerProfileId == owner.Id && item.PoiId == poiId && item.Status == "Pending", cancellationToken);

        if (pendingExists)
        {
            TempData["ErrorMessage"] = "Bạn đã gửi yêu cầu nhận POI này và đang chờ duyệt.";
            return RedirectToAction(nameof(Requests), "Poi", new { area = "Owner" });
        }

        _context.PoiOwnerRequests.Add(new PoiOwnerRequest
        {
            OwnerProfileId = owner.Id,
            PoiId = poiId,
            RequestType = "Claim",
            Status = "Pending",
            Note = string.IsNullOrWhiteSpace(note) ? "Chủ gian hàng yêu cầu nhận quyền quản lý POI." : note.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã gửi yêu cầu nhận POI. Admin sẽ kiểm tra và duyệt.";
        return RedirectToAction(nameof(Requests), "Poi", new { area = "Owner" });
    }

    [HttpGet]
    public async Task<IActionResult> Requests(CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        await NormalizeCompletedRequestsAsync(owner.Id, cancellationToken);

        var requests = await _context.PoiOwnerRequests
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Where(item => item.OwnerProfileId == owner.Id)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(requests);
    }

    private async Task NormalizeCompletedRequestsAsync(int ownerId, CancellationToken cancellationToken)
    {
        var completedRequests = await _context.PoiOwnerRequests
            .Include(item => item.Poi)
            .Where(item =>
                item.OwnerProfileId == ownerId &&
                item.Status == "Pending" &&
                item.Poi != null &&
                item.Poi.OwnerProfileId == ownerId &&
                item.Poi.Status == "Approved")
            .ToListAsync(cancellationToken);

        if (completedRequests.Count == 0)
            return;

        var reviewedAt = DateTime.UtcNow;
        foreach (var request in completedRequests)
        {
            request.Status = "Approved";
            request.ReviewedAt ??= reviewedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task PrepareCreateViewAsync(OwnerProfile owner, CancellationToken cancellationToken)
    {
        var canUseAi = await HasPremiumAiAsync(owner.Id, cancellationToken);
        ViewBag.CanUseOwnerAi = canUseAi;
        ViewBag.OwnerPlanName = await _context.OwnerSubscriptions
            .Include(item => item.PaymentPlan)
            .Where(item => item.OwnerProfileId == owner.Id && item.Status == "Active" && item.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(item => item.ExpiresAt)
            .Select(item => item.PaymentPlan != null ? item.PaymentPlan.PlanName : null)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> HasPremiumAiAsync(int ownerId, CancellationToken cancellationToken)
    {
        // SQLite hạn chế một số biểu thức decimal trong truy vấn.
        // Lấy subscription trước rồi kiểm tra Price/PlanCode ở phía C# cho an toàn.
        var activeSubscriptions = await _context.OwnerSubscriptions
            .Include(item => item.PaymentPlan)
            .Where(item =>
                item.OwnerProfileId == ownerId &&
                item.Status == "Active" &&
                item.ExpiresAt > DateTime.UtcNow &&
                item.PaymentPlan != null)
            .ToListAsync(cancellationToken);

        return activeSubscriptions.Any(item => OwnerFeaturedPlanHelper.IsFeaturedMapPlan(item.PaymentPlan));
    }

    private async Task<(string? WebUrl, string? PhysicalPath)> SaveUploadedFileAsync(
        IFormFile? file,
        string subfolder,
        CancellationToken cancellationToken)
    {
        if (file is not { Length: > 0 })
            return (null, null);

        var webRootPath = _hostEnvironment.WebRootPath
            ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");

        var uploadsFolder = Path.Combine(webRootPath, "uploads", subfolder);
        Directory.CreateDirectory(uploadsFolder);

        var extension = Path.GetExtension(Path.GetFileName(file.FileName));
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".bin";

        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

        await using var stream = new FileStream(physicalPath, FileMode.CreateNew);
        await file.CopyToAsync(stream, cancellationToken);

        return ($"/uploads/{subfolder}/{uniqueFileName}", physicalPath);
    }

    private static bool TryParseCoordinate(string? value, out decimal coordinate)
    {
        coordinate = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Replace(',', '.');
        return decimal.TryParse(
            normalized,
            NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out coordinate);
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
