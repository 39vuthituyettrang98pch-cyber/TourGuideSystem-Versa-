using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class TourController : Controller
{
    private readonly AppDbContext _context;

    public TourController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? searchString, int page = 1)
    {
        int pageSize = 10; // Giới hạn 10 Tour trên 1 trang
        var query = _context.Tours
            .Include(t => t.Translations)
            .Include(t => t.TourPois).ThenInclude(tp => tp.Poi).ThenInclude(p => p!.Translations)
            .AsSplitQuery()
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            var lowerSearch = searchString.ToLower();
            query = query.Where(t => t.Translations!.Any(tr => tr.Title.ToLower().Contains(lowerSearch)));
        }

        query = query.OrderByDescending(t => t.CreatedAt);
        
        var totalItems = await query.CountAsync();
        var tours = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.SearchString = searchString;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(tours);
    }

    public IActionResult Create()
    {
        return View(new Tour { EstimatedTime = 60 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Tour tour, string Title, string Description)
    {
        if (ModelState.IsValid)
        {
            tour.CreatedAt = DateTime.Now;
            _context.Add(tour);
            await _context.SaveChangesAsync();

            // Tự động thêm bản dịch Tiếng Việt ban đầu
            if (!string.IsNullOrEmpty(Title))
            {
                var translation = new TourTranslation
                {
                    TourId = tour.Id,
                    Title = Title,
                    Description = Description,
                    LanguageCode = "vi"
                };
                _context.TourTranslations.Add(translation);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
        return View(tour);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var tour = await _context.Tours.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (tour == null) return NotFound();

        return View(tour);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Tour tour)
    {
        if (id != tour.Id) return NotFound();

        if (!ModelState.IsValid)
            return View(tour);

        var existing = await _context.Tours.FindAsync(id);
        if (existing == null)
            return NotFound();

        existing.EstimatedTime = tour.EstimatedTime;
        existing.Status = tour.Status;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã cập nhật tour.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var tour = await _context.Tours.FindAsync(id);
        if (tour != null) _context.Tours.Remove(tour);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã xóa tour.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ExportExcel()
    {
        var tours = await _context.Tours
            .Include(t => t.Translations)
            .Include(t => t.TourPois)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var builder = new StringBuilder();
        builder.Append('\uFEFF'); // Hỗ trợ font tiếng Việt UTF-8 cho Excel
        builder.AppendLine("ID,Ten_Tour,Thoi_Gian_Phut,So_Luong_POI,Trang_Thai,Ngay_Tao");

        foreach (var tour in tours)
        {
            var title = tour.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Title ?? $"Tour #{tour.Id}";
            title = $"\"{title.Replace("\"", "\"\"")}\""; // Bọc ngoặc kép để tránh lỗi khi tên có chứa dấu phẩy
            var poiCount = tour.TourPois?.Count ?? 0;
            builder.AppendLine($"{tour.Id},{title},{tour.EstimatedTime},{poiCount},{tour.Status},{tour.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"DanhSach_Tour_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    // ==========================================
    // QUẢN LÝ POI TRONG TOUR
    // ==========================================
    public async Task<IActionResult> ManagePois(int id)
    {
        var tour = await _context.Tours
            .Include(t => t.TourPois).ThenInclude(tp => tp.Poi).ThenInclude(p => p!.Translations)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tour == null) return NotFound();

        var existingPoiIds = tour.TourPois?.Select(tp => tp.PoiId).ToList() ?? new List<int>();

        ViewBag.AvailablePois = await _context.Pois
            .Include(p => p.Translations)
            .Where(p => !existingPoiIds.Contains(p.Id))
            .ToListAsync();

        return View(tour);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPoi(int tourId, int poiId, int sequenceOrder)
    {
        var exists = await _context.TourPois.AnyAsync(tp => tp.TourId == tourId && tp.PoiId == poiId);
        if (!exists) {
            _context.TourPois.Add(new TourPoi
            {
                TourId = tourId,
                PoiId = poiId,
                SequenceOrder = Math.Max(1, sequenceOrder)
            });
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(ManagePois), new { id = tourId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePoi(int tourId, int poiId)
    {
        var tourPoi = await _context.TourPois.FirstOrDefaultAsync(tp => tp.TourId == tourId && tp.PoiId == poiId);
        if (tourPoi != null) {
            _context.TourPois.Remove(tourPoi);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(ManagePois), new { id = tourId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePoiSequence(int tourId, [FromBody] List<int> poiIdsInOrder)
    {
        var tourPois = await _context.TourPois.Where(tp => tp.TourId == tourId).ToListAsync();
        for (int i = 0; i < poiIdsInOrder.Count; i++)
        {
            var tp = tourPois.FirstOrDefault(x => x.PoiId == poiIdsInOrder[i]);
            if (tp != null)
            {
                tp.SequenceOrder = i + 1;
            }
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Đã cập nhật thứ tự POI thành công!" });
    }

    private bool TourExists(int id)
    {
        return _context.Tours.Any(e => e.Id == id);
    }
}
