using AdminWeb.Areas.Editor.Models;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Areas.Editor.Controllers;

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class WorkspaceController : Controller
{
    private readonly AppDbContext _context;

    public WorkspaceController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Board(string status = "All", string? q = null, string issue = "All", CancellationToken cancellationToken = default)
    {
        var activeLanguages = await GetActiveLanguagesAsync(cancellationToken);
        var query = BuildPoiQuery();

        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(poi => poi.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            var like = $"%{keyword}%";

            if (int.TryParse(keyword.TrimStart('#'), out var id))
            {
                query = query.Where(poi => poi.Id == id || poi.Translations.Any(translation => EF.Functions.Like(translation.Name, like)));
            }
            else
            {
                query = query.Where(poi => poi.Translations.Any(translation =>
                    EF.Functions.Like(translation.Name, like) ||
                    (translation.ShortDescription != null && EF.Functions.Like(translation.ShortDescription, like))));
            }
        }

        var pois = await query
            .OrderByDescending(poi => poi.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var items = pois.Select(poi => ToEditorCard(poi, activeLanguages)).ToList();

        items = issue switch
        {
            "MissingLanguage" => items.Where(item => item.MissingLanguageCount > 0).ToList(),
            "MissingAudio" => items.Where(item => item.MissingAudioCount > 0).ToList(),
            "Rejected" => items.Where(item => item.Status == "Rejected").ToList(),
            "LowQuality" => items.Where(item => item.QualityScore < 70).ToList(),
            _ => items
        };

        var counts = await BuildStatusCountsAsync(cancellationToken);
        var model = new EditorContentBoardViewModel
        {
            StatusFilter = status,
            Search = q,
            IssueFilter = issue,
            TotalCount = counts.Total,
            DraftCount = counts.Draft,
            PendingCount = counts.Pending,
            ApprovedCount = counts.Approved,
            RejectedCount = counts.Rejected,
            NeedsWorkCount = items.Count(item => item.Issues.Count > 0 || item.Status == "Rejected"),
            ActiveLanguages = activeLanguages,
            Items = items
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Quality(CancellationToken cancellationToken)
    {
        var activeLanguages = await GetActiveLanguagesAsync(cancellationToken);
        var pois = await BuildPoiQuery()
            .OrderByDescending(poi => poi.CreatedAt)
            .Take(250)
            .ToListAsync(cancellationToken);

        var items = pois
            .Select(poi => ToEditorCard(poi, activeLanguages))
            .OrderBy(item => item.QualityScore)
            .ThenByDescending(item => item.CreatedAt)
            .ToList();

        var model = new EditorQualityReportViewModel
        {
            TotalPois = items.Count,
            GoodPois = items.Count(item => item.QualityScore >= 85),
            WarningPois = items.Count(item => item.QualityScore is >= 60 and < 85),
            CriticalPois = items.Count(item => item.QualityScore < 60),
            ActiveLanguages = activeLanguages,
            Items = items
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(int id, string? note, CancellationToken cancellationToken)
    {
        var poi = await _context.Pois.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poi == null)
            return NotFound();

        poi.Status = "Pending";
        poi.AdminNote = string.IsNullOrWhiteSpace(note)
            ? "Editor đã gửi nội dung sang hàng chờ kiểm duyệt."
            : $"Editor gửi duyệt: {note.Trim()}";

        await LogAsync("EditorSubmitForReview", "pois", id, $"Editor gửi POI #{id} sang hàng chờ kiểm duyệt.", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Đã gửi POI #{id} sang hàng chờ kiểm duyệt.";
        return RedirectToAction(nameof(Board), "Workspace", new { area = "Editor" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveToDraft(int id, CancellationToken cancellationToken)
    {
        var poi = await _context.Pois.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poi == null)
            return NotFound();

        poi.Status = "Draft";
        poi.AdminNote = "Editor chuyển về bản nháp để chỉnh sửa.";

        await LogAsync("EditorMoveToDraft", "pois", id, $"Editor chuyển POI #{id} về bản nháp.", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Đã chuyển POI #{id} về bản nháp.";
        return RedirectToAction(nameof(Board), "Workspace", new { area = "Editor", status = "Draft" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateVietnameseToMissing(int id, CancellationToken cancellationToken)
    {
        var activeLanguages = await GetActiveLanguagesAsync(cancellationToken);
        var poi = await _context.Pois
            .Include(item => item.Translations)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (poi == null)
            return NotFound();

        var vi = poi.Translations.FirstOrDefault(item => item.LanguageCode == "vi")
            ?? poi.Translations.FirstOrDefault();

        if (vi == null)
        {
            TempData["ErrorMessage"] = "POI chưa có bản dịch nào để tạo bản nháp ngôn ngữ.";
            return RedirectToAction(nameof(Board), "Workspace", new { area = "Editor" });
        }

        var existing = poi.Translations.Select(item => item.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = activeLanguages.Where(code => !existing.Contains(code)).ToList();

        foreach (var languageCode in missing)
        {
            _context.PoiTranslations.Add(new PoiTranslation
            {
                PoiId = id,
                LanguageCode = languageCode,
                Name = languageCode == "vi" ? vi.Name : $"{vi.Name} ({languageCode.ToUpperInvariant()} cần dịch)",
                ShortDescription = vi.ShortDescription,
                FullDescription = vi.FullDescription,
                TtsScript = vi.TtsScript,
                UpdatedAt = DateTime.Now
            });
        }

        if (missing.Count > 0)
        {
            poi.Status = poi.Status == "Approved" ? "Pending" : poi.Status;
            poi.AdminNote = $"Editor đã tạo bản nháp cho ngôn ngữ còn thiếu: {string.Join(", ", missing)}.";
            await LogAsync("EditorCreateMissingTranslations", "pois", id, poi.AdminNote, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"Đã tạo {missing.Count} bản nháp ngôn ngữ cho POI #{id}.";
        }
        else
        {
            TempData["SuccessMessage"] = "POI đã đủ ngôn ngữ đang bật.";
        }

        return RedirectToAction(nameof(Board), "Workspace", new { area = "Editor" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QueueAudioTask(int id, CancellationToken cancellationToken)
    {
        var poiExists = await _context.Pois.AnyAsync(item => item.Id == id, cancellationToken);
        if (!poiExists)
            return NotFound();

        var hasRunningTask = await _context.MediaTasks.AnyAsync(task =>
            task.PoiId == id &&
            task.TaskType == MediaTaskType.TextToAudio &&
            (task.Status == MediaTaskStatus.Pending || task.Status == MediaTaskStatus.Processing),
            cancellationToken);

        if (hasRunningTask)
        {
            TempData["SuccessMessage"] = $"POI #{id} đã có tác vụ audio đang chờ hoặc đang xử lý.";
            return RedirectToAction(nameof(Board), "Workspace", new { area = "Editor", issue = "MissingAudio" });
        }

        _context.MediaTasks.Add(new MediaTask
        {
            PoiId = id,
            TaskType = MediaTaskType.TextToAudio,
            Status = MediaTaskStatus.Pending,
            ProgressPercentage = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await LogAsync("EditorQueueAudio", "media_tasks", id, $"Editor tạo tác vụ sinh audio cho POI #{id}.", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Đã thêm tác vụ sinh audio cho POI #{id}.";
        return RedirectToAction(nameof(Board), "Workspace", new { area = "Editor", issue = "MissingAudio" });
    }

    private IQueryable<Poi> BuildPoiQuery()
    {
        return _context.Pois
            .Include(item => item.Creator)
            .Include(item => item.OwnerProfile)
            .Include(item => item.Translations)
            .Include(item => item.MediaAssets)
            .Include(item => item.MediaTasks)
            .Include(item => item.PoiCategories)
            .ThenInclude(item => item.Category)
            .ThenInclude(item => item!.Translations);
    }

    private async Task<IReadOnlyList<string>> GetActiveLanguagesAsync(CancellationToken cancellationToken)
    {
        var languages = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(language => language.IsActive)
            .OrderBy(language => language.LanguageCode == "vi" ? 0 : 1)
            .ThenBy(language => language.LanguageCode)
            .Select(language => language.LanguageCode)
            .ToListAsync(cancellationToken);

        return languages.Count == 0 ? ["vi"] : languages;
    }

    private static EditorPoiCardViewModel ToEditorCard(Poi poi, IReadOnlyList<string> activeLanguages)
    {
        var translations = poi.Translations ?? [];
        var translationCodes = translations.Select(item => item.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingLanguages = activeLanguages.Where(code => !translationCodes.Contains(code)).ToList();
        var vi = translations.FirstOrDefault(item => item.LanguageCode == "vi") ?? translations.FirstOrDefault();
        var missingAudio = translations.Count(item => string.IsNullOrWhiteSpace(item.AudioUrl));
        var missingScript = translations.Count(item => string.IsNullOrWhiteSpace(item.TtsScript));
        var hasImage = !string.IsNullOrWhiteSpace(poi.CoverImageUrl) || poi.MediaAssets.Any(asset => asset.MediaType == "image");
        var issues = new List<string>();

        if (vi == null || string.IsNullOrWhiteSpace(vi.Name))
            issues.Add("Thiếu tên tiếng Việt");

        if (vi == null || string.IsNullOrWhiteSpace(vi.ShortDescription))
            issues.Add("Thiếu mô tả ngắn");

        if (vi == null || string.IsNullOrWhiteSpace(vi.FullDescription))
            issues.Add("Thiếu mô tả chi tiết");

        if (missingLanguages.Count > 0)
            issues.Add($"Thiếu {missingLanguages.Count} ngôn ngữ");

        if (missingAudio > 0)
            issues.Add($"Thiếu audio ở {missingAudio} bản dịch");

        if (missingScript > 0)
            issues.Add($"Thiếu script TTS ở {missingScript} bản dịch");

        if (!hasImage)
            issues.Add("Thiếu ảnh bìa/media");

        if (string.IsNullOrWhiteSpace(poi.QrCodeToken))
            issues.Add("Thiếu mã QR");

        if (poi.PoiCategories.Count == 0)
            issues.Add("Chưa gắn danh mục");

        if (poi.Latitude == 0 || poi.Longitude == 0)
            issues.Add("Tọa độ chưa hợp lệ");

        var score = 100;
        score -= missingLanguages.Count * 8;
        score -= missingAudio * 5;
        score -= missingScript * 3;
        score -= string.IsNullOrWhiteSpace(vi?.ShortDescription) ? 10 : 0;
        score -= string.IsNullOrWhiteSpace(vi?.FullDescription) ? 15 : 0;
        score -= hasImage ? 0 : 12;
        score -= poi.PoiCategories.Count > 0 ? 0 : 8;
        score -= string.IsNullOrWhiteSpace(poi.QrCodeToken) ? 10 : 0;
        score = Math.Clamp(score, 0, 100);

        return new EditorPoiCardViewModel
        {
            Id = poi.Id,
            Name = vi?.Name ?? $"POI #{poi.Id}",
            Status = poi.Status,
            AdminNote = poi.AdminNote,
            CreatorName = poi.Creator?.Username,
            CreatedAt = poi.CreatedAt,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            Radius = poi.Radius,
            TranslationCount = translations.Count,
            RequiredLanguageCount = activeLanguages.Count,
            MissingLanguageCount = missingLanguages.Count,
            MissingAudioCount = missingAudio,
            MissingScriptCount = missingScript,
            MediaCount = poi.MediaAssets.Count,
            CategoryCount = poi.PoiCategories.Count,
            QualityScore = score,
            MissingLanguages = missingLanguages,
            Issues = issues
        };
    }

    private async Task<(int Total, int Draft, int Pending, int Approved, int Rejected)> BuildStatusCountsAsync(CancellationToken cancellationToken)
    {
        var statuses = await _context.Pois
            .AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return (
            statuses.Sum(item => item.Count),
            statuses.Where(item => item.Status == "Draft").Sum(item => item.Count),
            statuses.Where(item => item.Status == "Pending").Sum(item => item.Count),
            statuses.Where(item => item.Status == "Approved").Sum(item => item.Count),
            statuses.Where(item => item.Status == "Rejected").Sum(item => item.Count));
    }

    private async Task LogAsync(string action, string targetTable, int targetId, string description, CancellationToken cancellationToken)
    {
        _context.AdminActivityLogs.Add(new AdminActivityLog
        {
            UserId = null,
            Action = action,
            TargetTable = targetTable,
            TargetId = targetId,
            Description = description,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.Now
        });

        await Task.CompletedTask;
    }
}
