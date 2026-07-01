using System.Text.Json;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
public class PoiTranslationController : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IGeminiService _geminiService;

    public PoiTranslationController(
        AppDbContext context,
        IWebHostEnvironment environment,
        IGeminiService geminiService)
    {
        _context = context;
        _environment = environment;
        _geminiService = geminiService;
    }

    public async Task<IActionResult> Index(int poiId, CancellationToken cancellationToken)
    {
        var poi = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .FirstOrDefaultAsync(item => item.Id == poiId, cancellationToken);
        if (poi == null)
            return NotFound();

        var existingCodes = poi.Translations
            .Select(item => NormalizeCode(item.LanguageCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activeLanguages = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.LanguageName)
            .ToListAsync(cancellationToken);

        var missingLanguages = activeLanguages
            .Where(item => !existingCodes.Contains(item.LanguageCode))
            .ToList();

        ViewBag.PoiId = poiId;
        ViewBag.MissingLanguages = missingLanguages;
        ViewBag.PoiName = poi.Translations
            .FirstOrDefault(item => item.LanguageCode == "vi")?.Name
            ?? poi.Translations.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Name))?.Name
            ?? $"POI #{poiId}";

        return View(poi.Translations.OrderBy(item => item.LanguageCode).ToList());
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var translation = await _context.PoiTranslations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return translation == null ? NotFound() : View(translation);
    }

    public async Task<IActionResult> Create(
        int poiId,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        if (!await _context.Pois.AnyAsync(item => item.Id == poiId, cancellationToken))
            return NotFound();

        await LoadLanguagesAsync(poiId, cancellationToken);

        var normalizedCode = NormalizeCode(languageCode);
        var availableLanguages = (IEnumerable<SupportedLanguage>)(ViewBag.Languages ?? Array.Empty<SupportedLanguage>());
        var selectedCode = availableLanguages.Any(item => item.LanguageCode == normalizedCode)
            ? normalizedCode
            : availableLanguages.FirstOrDefault()?.LanguageCode ?? string.Empty;

        return View(new PoiTranslation
        {
            PoiId = poiId,
            LanguageCode = selectedCode
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PoiTranslation translation,
        IFormFile? audioFile,
        bool autoTranslate,
        CancellationToken cancellationToken)
    {
        translation.LanguageCode = NormalizeCode(translation.LanguageCode);

        await ValidateAsync(translation, audioFile, null, cancellationToken);

        var shouldAutoTranslate = autoTranslate ||
            string.IsNullOrWhiteSpace(translation.Name) ||
            string.IsNullOrWhiteSpace(translation.FullDescription);

        if (shouldAutoTranslate && ModelState.IsValid)
        {
            try
            {
                var generated = await GeneratePoiTranslationAsync(
                    translation.PoiId,
                    translation.LanguageCode,
                    cancellationToken);

                translation.Name = generated.Name;
                translation.ShortDescription = generated.ShortDescription;
                translation.FullDescription = generated.FullDescription;
            }
            catch (Exception exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "AI chưa tạo được bản dịch. Bạn có thể nhập thủ công hoặc thử lại. Chi tiết: " + exception.Message);
            }
        }

        if (string.IsNullOrWhiteSpace(translation.Name))
            ModelState.AddModelError(nameof(translation.Name), "Tên hiển thị không được để trống.");

        if (!ModelState.IsValid)
        {
            await LoadLanguagesAsync(translation.PoiId, cancellationToken);
            return View(translation);
        }

        translation.Name = translation.Name.Trim();
        translation.ShortDescription = string.IsNullOrWhiteSpace(translation.ShortDescription)
            ? null
            : translation.ShortDescription.Trim();
        translation.FullDescription = string.IsNullOrWhiteSpace(translation.FullDescription)
            ? null
            : translation.FullDescription.Trim();
        translation.TtsScript = translation.FullDescription;
        translation.AudioUrl = await SaveAudioAsync(audioFile, cancellationToken);
        translation.UpdatedAt = DateTime.Now;

        _context.PoiTranslations.Add(translation);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = shouldAutoTranslate
            ? "Đã AI dịch và thêm bản dịch POI. Bạn nên kiểm tra lại tên riêng trước khi dùng chính thức."
            : "Đã thêm bản dịch POI.";

        return RedirectToAction(nameof(Index), new { poiId = translation.PoiId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoCreate(
        int poiId,
        string languageCode,
        CancellationToken cancellationToken)
    {
        languageCode = NormalizeCode(languageCode);

        var translation = new PoiTranslation
        {
            PoiId = poiId,
            LanguageCode = languageCode
        };

        await ValidateAsync(translation, null, null, cancellationToken);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = string.Join(" ", ModelState.Values
                .SelectMany(item => item.Errors)
                .Select(item => item.ErrorMessage));
            return RedirectToAction(nameof(Index), new { poiId });
        }

        try
        {
            var generated = await GeneratePoiTranslationAsync(poiId, languageCode, cancellationToken);

            translation.Name = generated.Name.Trim();
            translation.ShortDescription = string.IsNullOrWhiteSpace(generated.ShortDescription)
                ? null
                : generated.ShortDescription.Trim();
            translation.FullDescription = string.IsNullOrWhiteSpace(generated.FullDescription)
                ? null
                : generated.FullDescription.Trim();
            translation.TtsScript = translation.FullDescription;
            translation.UpdatedAt = DateTime.Now;

            _context.PoiTranslations.Add(translation);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Đã AI dịch nhanh bản dịch POI. Bạn nên kiểm tra lại tên riêng trước khi dùng chính thức.";
        }
        catch (Exception exception)
        {
            TempData["ErrorMessage"] = "AI chưa tạo được bản dịch: " + exception.Message;
        }

        return RedirectToAction(nameof(Index), new { poiId });
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id == null)
            return NotFound();

        var translation = await _context.PoiTranslations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return translation == null ? NotFound() : View(translation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        PoiTranslation input,
        IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        if (id != input.Id)
            return NotFound();

        input.LanguageCode = NormalizeCode(input.LanguageCode);
        await ValidateAsync(input, audioFile, input.Id, cancellationToken);
        if (!ModelState.IsValid)
            return View(input);

        var translation = await _context.PoiTranslations.FindAsync([id], cancellationToken);
        if (translation == null)
            return NotFound();

        translation.LanguageCode = input.LanguageCode;
        translation.Name = input.Name.Trim();
        translation.ShortDescription = input.ShortDescription;
        translation.FullDescription = input.FullDescription;
        translation.TtsScript = input.FullDescription;
        translation.UpdatedAt = DateTime.Now;

        var audioUrl = await SaveAudioAsync(audioFile, cancellationToken);
        if (audioUrl != null)
            translation.AudioUrl = audioUrl;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã cập nhật bản dịch POI.";
        return RedirectToAction(nameof(Index), new { poiId = translation.PoiId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, int poiId, CancellationToken cancellationToken)
    {
        var translation = await _context.PoiTranslations.FindAsync([id], cancellationToken);
        if (translation == null)
            return NotFound();

        if (translation.LanguageCode == "vi")
        {
            TempData["ErrorMessage"] = "Không thể xóa nội dung tiếng Việt gốc.";
            return RedirectToAction(nameof(Index), new { poiId });
        }

        _context.PoiTranslations.Remove(translation);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã xóa bản dịch POI.";
        return RedirectToAction(nameof(Index), new { poiId });
    }

    private async Task ValidateAsync(
        PoiTranslation translation,
        IFormFile? audioFile,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        translation.LanguageCode = NormalizeCode(translation.LanguageCode);

        if (!await _context.Pois.AnyAsync(item => item.Id == translation.PoiId, cancellationToken))
            ModelState.AddModelError(nameof(translation.PoiId), "POI không tồn tại.");

        if (string.IsNullOrWhiteSpace(translation.LanguageCode))
        {
            ModelState.AddModelError(nameof(translation.LanguageCode), "Vui lòng chọn ngôn ngữ.");
        }
        else if (!await _context.SupportedLanguages.AnyAsync(
                     item => item.IsActive && item.LanguageCode == translation.LanguageCode,
                     cancellationToken))
        {
            ModelState.AddModelError(nameof(translation.LanguageCode), "Ngôn ngữ này chưa được bật trong danh sách ngôn ngữ hỗ trợ.");
        }

        if (await _context.PoiTranslations.AnyAsync(
                item => item.PoiId == translation.PoiId
                    && item.LanguageCode == translation.LanguageCode
                    && (!excludingId.HasValue || item.Id != excludingId.Value),
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(translation.LanguageCode),
                "POI đã có bản dịch cho ngôn ngữ này.");
        }

        if (audioFile is { Length: > 0 } &&
            !audioFile.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(audioFile), "File tải lên phải là audio.");
        }
    }

    private async Task LoadLanguagesAsync(int poiId, CancellationToken cancellationToken)
    {
        var existingCodes = await _context.PoiTranslations
            .AsNoTracking()
            .Where(item => item.PoiId == poiId)
            .Select(item => item.LanguageCode)
            .ToListAsync(cancellationToken);

        var existingSet = existingCodes
            .Select(NormalizeCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activeLanguages = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.LanguageName)
            .ToListAsync(cancellationToken);

        ViewBag.Languages = activeLanguages
            .Where(item => !existingSet.Contains(item.LanguageCode))
            .ToList();
    }

    private async Task<GeneratedPoiTranslation> GeneratePoiTranslationAsync(
        int poiId,
        string languageCode,
        CancellationToken cancellationToken)
    {
        languageCode = NormalizeCode(languageCode);

        var language = await _context.SupportedLanguages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.IsActive && item.LanguageCode == languageCode,
                cancellationToken)
            ?? throw new InvalidOperationException("Ngôn ngữ chưa được bật trong hệ thống.");

        var source = await _context.PoiTranslations
            .AsNoTracking()
            .Where(item => item.PoiId == poiId)
            .OrderByDescending(item => item.LanguageCode == "vi")
            .ThenByDescending(item => item.LanguageCode == "en")
            .FirstOrDefaultAsync(item => item.Name != "", cancellationToken)
            ?? throw new InvalidOperationException("POI chưa có bản gốc để dịch.");

        var sourceJson = JsonSerializer.Serialize(new
        {
            name = source.Name,
            shortDescription = source.ShortDescription ?? string.Empty,
            fullDescription = source.FullDescription ?? string.Empty
        });

        var prompt =
            "Bạn là biên dịch viên nội dung du lịch cho hệ thống tour guide.\n" +
            $"Hãy dịch nội dung POI sang ngôn ngữ đích: {language.LanguageName} ({language.LanguageCode}).\n" +
            "Yêu cầu quan trọng:\n" +
            "- Dịch field name sang đúng ngôn ngữ đích. Với địa danh nổi tiếng, dùng tên bản địa/tên phổ biến của ngôn ngữ đó.\n" +
            "- Không giữ nguyên tiếng Anh/tiếng Việt cho name nếu có bản dịch phổ biến.\n" +
            "- Nếu name là thương hiệu/tên quán/tên riêng không nên dịch, hãy giữ tên gốc.\n" +
            "- Nếu không chắc, dùng tên đã dịch và thêm tên gốc trong ngoặc.\n" +
            "- Giữ đúng ý, không bịa thêm dữ kiện mới. Văn phong tự nhiên cho ứng dụng du lịch.\n" +
            "- Trả về DUY NHẤT JSON object, không markdown, không giải thích.\n" +
            "Schema bắt buộc: {\"name\":\"...\",\"shortDescription\":\"...\",\"fullDescription\":\"...\"}\n" +
            "Dữ liệu nguồn:\n" +
            sourceJson;

        var response = await _geminiService.GenerateTextAsync(prompt, cancellationToken);
        var json = ExtractFirstJsonObject(response);
        var translated = JsonSerializer.Deserialize<GeneratedPoiTranslation>(json, JsonOptions)
            ?? throw new InvalidOperationException("AI trả về JSON bản dịch không hợp lệ.");

        if (string.IsNullOrWhiteSpace(translated.Name))
            throw new InvalidOperationException("AI chưa trả về tên POI.");

        return new GeneratedPoiTranslation(
            translated.Name.Trim(),
            translated.ShortDescription?.Trim() ?? string.Empty,
            translated.FullDescription?.Trim() ?? string.Empty);
    }

    private async Task<string?> SaveAudioAsync(
        IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        if (audioFile is not { Length: > 0 })
            return null;

        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(webRoot, "uploads", "audio");
        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(Path.GetFileName(audioFile.FileName));
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using var stream = new FileStream(
            Path.Combine(directory, fileName),
            FileMode.CreateNew);
        await audioFile.CopyToAsync(stream, cancellationToken);
        return $"/uploads/audio/{fileName}";
    }

    private static string ExtractFirstJsonObject(string text)
    {
        var cleaned = (text ?? string.Empty)
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        var start = cleaned.IndexOf('{');
        if (start < 0)
            throw new InvalidOperationException("AI không trả về JSON object hợp lệ.");

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < cleaned.Length; i++)
        {
            var c = cleaned[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
                depth++;
            else if (c == '}')
                depth--;

            if (depth == 0)
                return cleaned.Substring(start, i - start + 1);
        }

        throw new InvalidOperationException("AI trả về JSON object chưa hoàn chỉnh.");
    }

    private static string NormalizeCode(string? code)
    {
        return (code ?? string.Empty).Trim().ToLowerInvariant();
    }

    private sealed record GeneratedPoiTranslation(
        string Name,
        string ShortDescription,
        string FullDescription);
}
