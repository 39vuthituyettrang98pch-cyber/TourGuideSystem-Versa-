using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using AdminWeb.ViewModels;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public class AnalyticsController : Controller
{
    private readonly AppDbContext _context;

    public AnalyticsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Lấy tọa độ của du khách để vẽ Heatmap
        var heatmapData = await _context.VisitorPlaybackLogs
            .Where(l => l.VisitorLatitude != null && l.VisitorLongitude != null)
            .Select(l => new { lat = l.VisitorLatitude, lng = l.VisitorLongitude, intensity = 1 })
            .ToListAsync();

        ViewBag.HeatmapData = heatmapData;
        ViewBag.TotalLogs = await _context.VisitorPlaybackLogs.CountAsync();

        var mapPois = await _context.Pois
            .AsNoTracking()
            .Include(poi => poi.Translations)
            .Where(poi =>
                poi.Latitude >= -90 && poi.Latitude <= 90 &&
                poi.Longitude >= -180 && poi.Longitude <= 180)
            .OrderBy(poi => poi.Id)
            .ToListAsync();
        ViewBag.PoiMapData = mapPois.Select(poi => new PoiMapItemViewModel
        {
            Id = poi.Id,
            Name = poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")?.Name
                ?? poi.Translations.FirstOrDefault()?.Name
                ?? $"POI #{poi.Id}",
            Latitude = (double)poi.Latitude,
            Longitude = (double)poi.Longitude,
            Radius = poi.Radius,
            Status = poi.Status
        }).ToList();
        ViewBag.TotalPois = mapPois.Count;

        // Dữ liệu cho biểu đồ Lượt nghe 7 ngày qua
        var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);
        var dailyLogs = await _context.VisitorPlaybackLogs
            .Where(l => l.CreatedAt >= sevenDaysAgo)
            .GroupBy(l => l.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var chartLabels = new List<string>();
        var chartData = new List<int>();

        for (int i = 0; i < 7; i++)
        {
            var date = sevenDaysAgo.AddDays(i);
            chartLabels.Add(date.ToString("dd/MM"));
            
            var match = dailyLogs.FirstOrDefault(d => d.Date == date);
            chartData.Add(match?.Count ?? 0);
        }

        ViewBag.ChartLabels = chartLabels;
        ViewBag.ChartData = chartData;

        // Dữ liệu cho biểu đồ tròn (Tỷ lệ ngôn ngữ)
        var languageStats = await _context.VisitorPlaybackLogs
            .GroupBy(l => l.LanguageCode)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.PieLabels = languageStats.Select(s => (s.Language ?? "unknown").ToUpper()).ToList();
        ViewBag.PieData = languageStats.Select(s => s.Count).ToList();

        // Dữ liệu cho Top POI (Nhóm theo PoiId vì PoiName là [NotMapped] không thể Query SQL)
        var topPoisData = await _context.VisitorPlaybackLogs
            .GroupBy(l => l.PoiId)
            .Select(g => new { PoiId = g.Key, ListenCount = g.Count() })
            .OrderByDescending(x => x.ListenCount)
            .Take(10)
            .ToListAsync();
            
        var topPoisDict = new Dictionary<string, int>();
        foreach (var item in topPoisData)
        {
            var poi = await _context.Pois
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == item.PoiId);
                
            var poiName = poi?.Translations?.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? $"POI #{item.PoiId}";
            topPoisDict[poiName] = item.ListenCount;
        }

        ViewBag.TopPois = topPoisDict;

        return View();
    }

    public async Task<IActionResult> ExportExcel()
    {
        var logs = await _context.VisitorPlaybackLogs
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        var builder = new StringBuilder();
        // Thêm BOM để Excel tự động nhận diện đúng font UTF-8 (Tiếng Việt)
        builder.Append('\uFEFF');
        builder.AppendLine("Thoi_Gian,Thiet_Bi,Ngon_Ngu,Kich_Hoat,Thoi_Luong_Giay,Vi_Do,Kinh_Do");

        foreach (var log in logs)
        {
            builder.AppendLine($"{log.CreatedAt:yyyy-MM-dd HH:mm:ss},{log.DeviceId},{log.LanguageCode},{log.TriggerType},{log.ListenDuration},{log.VisitorLatitude},{log.VisitorLongitude}");
        }

        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"BaoCao_VERSA_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }
}
