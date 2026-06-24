using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
public sealed class MenuItemsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public MenuItemsController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index(int? poiId, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var ownedPois = await GetOwnedPoisAsync(owner.Id, cancellationToken);
        ViewBag.Pois = ownedPois;
        ViewBag.SelectedPoiId = poiId;

        var query = _context.OwnerMenuItems
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Where(item => item.OwnerProfileId == owner.Id);

        if (poiId.HasValue && poiId.Value > 0)
            query = query.Where(item => item.PoiId == poiId.Value);

        var items = await query
            .OrderBy(item => item.PoiId)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? poiId, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        ViewBag.Pois = await GetOwnedPoisAsync(owner.Id, cancellationToken);
        ViewBag.SelectedPoiId = poiId;
        return View(new OwnerMenuItem { PoiId = poiId ?? 0, Price = 0, Currency = "VND", Status = "Active" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_485_760)]
    public async Task<IActionResult> Create(OwnerMenuItem model, IFormFile? imageFile, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        if (!await _context.Pois.AnyAsync(poi => poi.Id == model.PoiId && poi.OwnerProfileId == owner.Id, cancellationToken))
            ModelState.AddModelError(nameof(model.PoiId), "POI không thuộc quyền quản lý của bạn.");

        model.Name = (model.Name ?? string.Empty).Trim();
        model.Description = model.Description?.Trim();
        model.ImageUrl = model.ImageUrl?.Trim();
        model.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "VND" : model.Currency.Trim().ToUpperInvariant();
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status.Trim();

        ValidateMenuImage(imageFile);

        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Vui lòng nhập tên món/sản phẩm/dịch vụ.");

        if (model.Price < 0)
            ModelState.AddModelError(nameof(model.Price), "Giá không được âm.");

        if (!ModelState.IsValid)
        {
            ViewBag.Pois = await GetOwnedPoisAsync(owner.Id, cancellationToken);
            ViewBag.SelectedPoiId = model.PoiId;
            return View(model);
        }

        var uploadedImageUrl = await SaveMenuImageAsync(imageFile, cancellationToken);
        if (!string.IsNullOrWhiteSpace(uploadedImageUrl))
            model.ImageUrl = uploadedImageUrl;

        model.OwnerProfileId = owner.Id;
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;

        _context.OwnerMenuItems.Add(model);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã thêm món/sản phẩm vào gian hàng.";
        return RedirectToAction(nameof(Index), "MenuItems", new { area = "Owner", poiId = model.PoiId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var item = await _context.OwnerMenuItems
            .FirstOrDefaultAsync(menu => menu.Id == id && menu.OwnerProfileId == owner.Id, cancellationToken);

        if (item == null)
            return NotFound();

        ViewBag.Pois = await GetOwnedPoisAsync(owner.Id, cancellationToken);
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_485_760)]
    public async Task<IActionResult> Edit(int id, OwnerMenuItem model, IFormFile? imageFile, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var item = await _context.OwnerMenuItems
            .FirstOrDefaultAsync(menu => menu.Id == id && menu.OwnerProfileId == owner.Id, cancellationToken);

        if (item == null)
            return NotFound();

        if (!await _context.Pois.AnyAsync(poi => poi.Id == model.PoiId && poi.OwnerProfileId == owner.Id, cancellationToken))
            ModelState.AddModelError(nameof(model.PoiId), "POI không thuộc quyền quản lý của bạn.");

        model.Name = (model.Name ?? string.Empty).Trim();
        model.Description = model.Description?.Trim();
        model.ImageUrl = model.ImageUrl?.Trim();
        model.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "VND" : model.Currency.Trim().ToUpperInvariant();
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status.Trim();

        ValidateMenuImage(imageFile);

        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Vui lòng nhập tên món/sản phẩm/dịch vụ.");

        if (model.Price < 0)
            ModelState.AddModelError(nameof(model.Price), "Giá không được âm.");

        if (!ModelState.IsValid)
        {
            ViewBag.Pois = await GetOwnedPoisAsync(owner.Id, cancellationToken);
            return View(model);
        }

        item.PoiId = model.PoiId;
        item.Name = model.Name;
        item.Description = model.Description;
        var uploadedImageUrl = await SaveMenuImageAsync(imageFile, cancellationToken);

        item.Price = model.Price;
        item.Currency = model.Currency;
        item.ImageUrl = !string.IsNullOrWhiteSpace(uploadedImageUrl) ? uploadedImageUrl : model.ImageUrl;
        item.Status = model.Status;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã cập nhật món/sản phẩm.";
        return RedirectToAction(nameof(Index), "MenuItems", new { area = "Owner", poiId = item.PoiId });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var item = await _context.OwnerMenuItems
            .FirstOrDefaultAsync(menu => menu.Id == id && menu.OwnerProfileId == owner.Id, cancellationToken);

        if (item == null)
            return NotFound();

        item.Status = item.Status == "Active" ? "Hidden" : "Active";
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = item.Status == "Active"
            ? "Đã hiện sản phẩm trên web du khách."
            : "Đã ẩn sản phẩm khỏi web du khách.";

        return RedirectToAction(nameof(Index), "MenuItems", new { area = "Owner", poiId = item.PoiId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var item = await _context.OwnerMenuItems
            .FirstOrDefaultAsync(menu => menu.Id == id && menu.OwnerProfileId == owner.Id, cancellationToken);

        if (item == null)
            return NotFound();

        var poiId = item.PoiId;
        _context.OwnerMenuItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã xóa món/sản phẩm.";
        return RedirectToAction(nameof(Index), "MenuItems", new { area = "Owner", poiId });
    }

    private void ValidateMenuImage(IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
            return;

        if (imageFile.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError("imageFile", "Ảnh món ăn tối đa 5MB.");
            return;
        }

        if (string.IsNullOrWhiteSpace(imageFile.ContentType) ||
            !imageFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("imageFile", "File tải lên phải là ảnh.");
            return;
        }

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg"
        };

        if (!allowedExtensions.Contains(extension))
        {
            ModelState.AddModelError("imageFile", "Ảnh chỉ hỗ trợ JPG, PNG, WEBP, GIF hoặc SVG.");
        }
    }

    private async Task<string?> SaveMenuImageAsync(IFormFile? imageFile, CancellationToken cancellationToken)
    {
        if (imageFile == null || imageFile.Length == 0)
            return null;

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var uploadRoot = Path.Combine(webRoot, "uploads", "menu-items");
        Directory.CreateDirectory(uploadRoot);

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = imageFile.ContentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "image/svg+xml" => ".svg",
                _ => ".img"
            };
        }

        var fileName = $"menu-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadRoot, fileName);

        await using var stream = System.IO.File.Create(physicalPath);
        await imageFile.CopyToAsync(stream, cancellationToken);

        return $"/uploads/menu-items/{fileName}";
    }

    private async Task<List<Poi>> GetOwnedPoisAsync(int ownerId, CancellationToken cancellationToken)
    {
        return await _context.Pois
            .Include(poi => poi.Translations)
            .Where(poi => poi.OwnerProfileId == ownerId)
            .OrderBy(poi => poi.Id)
            .ToListAsync(cancellationToken);
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
