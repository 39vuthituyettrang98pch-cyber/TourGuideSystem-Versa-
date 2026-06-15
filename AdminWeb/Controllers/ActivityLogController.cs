using AdminWeb.Data;
using AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public class ActivityLogController : Controller
{
    private readonly AppDbContext _context;

    public ActivityLogController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? searchString, int page = 1)
    {
        int pageSize = 15; // Hiển thị 15 dòng trên 1 trang
        var query = _context.VisitorPlaybackLogs
            .Include(l => l.Poi)
            .ThenInclude(p => p!.Translations)
            .AsQueryable();

        // Lọc theo ID Thiết bị
        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(l => l.DeviceId.Contains(searchString));
        }

        query = query.OrderByDescending(l => l.CreatedAt);
        
        var totalItems = await query.CountAsync();
        var logs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            
        foreach (var log in logs)
        {
            log.PoiName = log.Poi?.Translations?.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? $"POI #{log.PoiId}";
        }

        ViewBag.SearchString = searchString;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(new UsageHistoryViewModel { Logs = logs });
    }

    public async Task<IActionResult> ExportExcel()
    {
        var logs = await _context.VisitorPlaybackLogs
            .Include(l => l.Poi)
            .ThenInclude(p => p!.Translations)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        var builder = new StringBuilder();
        builder.Append('\uFEFF'); // Hỗ trợ font tiếng Việt UTF-8 cho Excel
        builder.AppendLine("Thoi_Gian,Thiet_Bi,Diem_Tham_Quan,Ngon_Ngu,Kich_Hoat,Vi_Tri,Thoi_Luong");

        foreach (var log in logs)
        {
            var poiName = log.Poi?.Translations?.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? $"POI #{log.PoiId}";
            poiName = $"\"{poiName.Replace("\"", "\"\"")}\""; // Bọc ngoặc kép để tránh lỗi khi tên có chứa dấu phẩy
            builder.AppendLine($"{log.CreatedAt:yyyy-MM-dd HH:mm:ss},{log.DeviceId},{poiName},{log.LanguageCode},{log.TriggerType},\"{log.VisitorLatitude}, {log.VisitorLongitude}\",{log.ListenDuration}");
        }

        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"NhatKyHoatDong_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }
}
