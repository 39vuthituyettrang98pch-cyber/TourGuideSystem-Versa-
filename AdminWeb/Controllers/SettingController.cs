using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public class SettingController : Controller
{
    private readonly AppDbContext _context;

    public SettingController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        int pageSize = 10; // Giới hạn 10 cấu hình trên 1 trang
        
        var query = _context.SystemSettings.AsQueryable();
        var totalItems = await query.CountAsync();
        
        var settingsList = await query
            .OrderByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Vẫn phải lấy toàn bộ cấu hình vào Dictionary để load giá trị cho phần "Cấu hình nhanh"
        var allSettingsList = await _context.SystemSettings.ToListAsync();
        var settings = allSettingsList.ToDictionary(s => s.SettingKey, s => s.SettingValue);

        ViewBag.GpsSensitivity = settings.GetValueOrDefault("GpsSensitivity", "15");
        ViewBag.CooldownTimer = settings.GetValueOrDefault("CooldownTimer", "120");
        ViewBag.DefaultLanguage = settings.GetValueOrDefault("DefaultLanguage", "vi");
        ViewBag.EnableAnalytics = settings.GetValueOrDefault("EnableAnalytics", "true");
        ViewBag.Languages = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(language => language.IsActive)
            .OrderBy(language => language.LanguageName)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return View(settingsList);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string GpsSensitivity, string CooldownTimer, string DefaultLanguage, string EnableAnalytics)
    {
        if (!int.TryParse(GpsSensitivity, out var gpsSensitivity) || gpsSensitivity <= 0)
            ModelState.AddModelError(nameof(GpsSensitivity), "Độ nhạy GPS phải lớn hơn 0.");
        if (!int.TryParse(CooldownTimer, out var cooldownTimer) || cooldownTimer < 0)
            ModelState.AddModelError(nameof(CooldownTimer), "Thời gian chờ không hợp lệ.");
        if (DefaultLanguage != "vi" &&
            !await _context.SupportedLanguages.AnyAsync(
                language => language.LanguageCode == DefaultLanguage && language.IsActive))
        {
            ModelState.AddModelError(nameof(DefaultLanguage), "Ngôn ngữ mặc định không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = string.Join(
                " ",
                ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        EnableAnalytics = string.Equals(EnableAnalytics, "true", StringComparison.OrdinalIgnoreCase)
            ? "true"
            : "false";
        await UpdateSetting("GpsSensitivity", gpsSensitivity.ToString(), "Độ nhạy GPS / Bán kính sai số bù trừ (mét)");
        await UpdateSetting("CooldownTimer", cooldownTimer.ToString(), "Thời gian chờ (Cooldown) phát lại Audio (giây)");
        await UpdateSetting("DefaultLanguage", DefaultLanguage, "Ngôn ngữ mặc định cho khách mới");
        await UpdateSetting("EnableAnalytics", EnableAnalytics, "Thu thập hành vi phân tích (Analytics)");

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã cập nhật cấu hình hệ thống thành công!";
        return RedirectToAction(nameof(Index));
    }

    private async Task UpdateSetting(string key, string value, string description)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (setting == null)
        {
            _context.SystemSettings.Add(new SystemSetting { SettingKey = key, SettingValue = value, Description = description });
        }
        else
        {
            setting.SettingValue = value;
            _context.SystemSettings.Update(setting);
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("SettingKey,SettingValue,Description")] SystemSetting systemSetting)
    {
        systemSetting.SettingKey = systemSetting.SettingKey.Trim();
        if (await _context.SystemSettings.AnyAsync(item => item.SettingKey == systemSetting.SettingKey))
            ModelState.AddModelError(nameof(systemSetting.SettingKey), "Khóa cấu hình đã tồn tại.");

        if (ModelState.IsValid)
        {
            _context.Add(systemSetting);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thêm cấu hình mới thành công!";
            return RedirectToAction(nameof(Index));
        }
        return View(systemSetting);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var systemSetting = await _context.SystemSettings.FindAsync(id);
        if (systemSetting == null)
        {
            return NotFound();
        }
        return View(systemSetting);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,SettingKey,SettingValue,Description")] SystemSetting systemSetting)
    {
        if (id != systemSetting.Id)
        {
            return NotFound();
        }

        systemSetting.SettingKey = systemSetting.SettingKey.Trim();
        if (await _context.SystemSettings.AnyAsync(
                item => item.SettingKey == systemSetting.SettingKey && item.Id != id))
        {
            ModelState.AddModelError(nameof(systemSetting.SettingKey), "Khóa cấu hình đã tồn tại.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(systemSetting);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật cấu hình thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SystemSettingExists(systemSetting.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(systemSetting);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var systemSetting = await _context.SystemSettings.FindAsync(id);
        if (systemSetting != null)
        {
            _context.SystemSettings.Remove(systemSetting);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa cấu hình thành công!";
        }
        
        return RedirectToAction(nameof(Index));
    }

    private bool SystemSettingExists(int id)
    {
        return _context.SystemSettings.Any(e => e.Id == id);
    }
}
