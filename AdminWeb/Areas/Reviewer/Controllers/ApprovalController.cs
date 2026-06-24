using AdminWeb.Areas.Reviewer.Models;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Reviewer.Controllers;

[Area("Reviewer")]
[Authorize(Policy = "ReviewerAreaPolicy")]
public sealed class ApprovalController : Controller
{
    private readonly AppDbContext _context;

    public ApprovalController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> PoiPending(string? q = null, CancellationToken cancellationToken = default)
    {
        var activeLanguages = await GetActiveLanguagesAsync(cancellationToken);
        var query = BuildPoiQuery().Where(poi => poi.Status == "Pending");

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
                query = query.Where(poi => poi.Translations.Any(translation => EF.Functions.Like(translation.Name, like)));
            }
        }

        var pois = await query
            .OrderBy(poi => poi.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var counts = await BuildCountsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var model = new ReviewerApprovalQueueViewModel
        {
            StatusFilter = "Pending",
            Search = q,
            PendingCount = counts.Pending,
            ApprovedCount = counts.Approved,
            RejectedCount = counts.Rejected,
            StalePendingCount = pois.Count(poi => (now - poi.CreatedAt.ToUniversalTime()).TotalDays >= 3),
            Items = pois.Select(poi => ToReviewItem(poi, activeLanguages)).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var activeLanguages = await GetActiveLanguagesAsync(cancellationToken);
        var poi = await BuildPoiQuery()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (poi == null)
            return NotFound();

        var reviews = await _context.PoiReviews
            .AsNoTracking()
            .Where(review => review.PoiId == id)
            .OrderByDescending(review => review.CreatedAt)
            .Take(8)
            .ToListAsync(cancellationToken);

        var card = ToReviewItem(poi, activeLanguages);
        var model = new ReviewerPoiDetailsViewModel
        {
            Poi = poi,
            Name = card.Name,
            QualityScore = card.QualityScore,
            ActiveLanguages = activeLanguages,
            MissingLanguages = activeLanguages
                .Where(code => poi.Translations.All(translation => translation.LanguageCode != code))
                .ToList(),
            Issues = card.Issues,
            RecentReviews = reviews
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> History(string status = "All", string? q = null, CancellationToken cancellationToken = default)
    {
        var activeLanguages = await GetActiveLanguagesAsync(cancellationToken);
        var query = BuildPoiQuery()
            .Where(poi => poi.Status == "Approved" || poi.Status == "Rejected");

        if (status is "Approved" or "Rejected")
        {
            query = query.Where(poi => poi.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            var like = $"%{keyword}%";
            query = query.Where(poi => poi.Translations.Any(translation => EF.Functions.Like(translation.Name, like)));
        }

        var pois = await query
            .OrderByDescending(poi => poi.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var counts = await BuildCountsAsync(cancellationToken);
        var model = new ReviewerApprovalQueueViewModel
        {
            StatusFilter = status,
            Search = q,
            PendingCount = counts.Pending,
            ApprovedCount = counts.Approved,
            RejectedCount = counts.Rejected,
            StalePendingCount = 0,
            Items = pois.Select(poi => ToReviewItem(poi, activeLanguages)).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PoiApprove(int id, CancellationToken cancellationToken)
    {
        var poi = await _context.Pois.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poi == null)
            return NotFound();

        if (!string.Equals(poi.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = $"POI #{id} đã được xử lý và không thể phê duyệt lại.";
            return RedirectToAction(nameof(Details), "Approval", new { area = "Reviewer", id });
        }

        poi.Status = "Approved";
        poi.AdminNote = null;
        await SyncOwnerRequestsAfterPoiReviewAsync(poi.Id, "Approved", null, cancellationToken);

        await LogAsync("ReviewerApprovePoi", "pois", id, $"Reviewer phê duyệt POI #{id}.", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Đã phê duyệt POI #{id}.";
        return RedirectToAction(nameof(PoiPending), "Approval", new { area = "Reviewer" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PoiReject(int id, string adminNote, CancellationToken cancellationToken)
    {
        var poi = await _context.Pois.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (poi == null)
            return NotFound();

        if (!string.Equals(poi.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = $"POI #{id} đã được xử lý và không thể từ chối lại.";
            return RedirectToAction(nameof(Details), "Approval", new { area = "Reviewer", id });
        }

        if (string.IsNullOrWhiteSpace(adminNote) || adminNote.Trim().Length < 5)
        {
            TempData["ErrorMessage"] = "Khi từ chối, vui lòng nhập lý do rõ ràng ít nhất 5 ký tự.";
            return RedirectToAction(nameof(Details), "Approval", new { area = "Reviewer", id });
        }

        poi.Status = "Rejected";
        poi.AdminNote = adminNote.Trim();
        await SyncOwnerRequestsAfterPoiReviewAsync(poi.Id, "Rejected", poi.AdminNote, cancellationToken);

        await LogAsync("ReviewerRejectPoi", "pois", id, $"Reviewer từ chối POI #{id}: {poi.AdminNote}", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Đã từ chối POI #{id}.";
        return RedirectToAction(nameof(PoiPending), "Approval", new { area = "Reviewer" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApprove(int[] selectedIds, CancellationToken cancellationToken)
    {
        if (selectedIds.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một POI.";
            return RedirectToAction(nameof(PoiPending), "Approval", new { area = "Reviewer" });
        }

        var pois = await _context.Pois
            .Where(poi => selectedIds.Contains(poi.Id) && poi.Status == "Pending")
            .ToListAsync(cancellationToken);

        foreach (var poi in pois)
        {
            poi.Status = "Approved";
            poi.AdminNote = null;
            await SyncOwnerRequestsAfterPoiReviewAsync(poi.Id, "Approved", null, cancellationToken);
            await LogAsync("ReviewerBulkApprovePoi", "pois", poi.Id, $"Reviewer duyệt nhanh POI #{poi.Id}.", cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Đã phê duyệt nhanh {pois.Count} POI.";
        return RedirectToAction(nameof(PoiPending), "Approval", new { area = "Reviewer" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkReject(int[] selectedIds, string adminNote, CancellationToken cancellationToken)
    {
        if (selectedIds.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một POI.";
            return RedirectToAction(nameof(PoiPending), "Approval", new { area = "Reviewer" });
        }

        if (string.IsNullOrWhiteSpace(adminNote) || adminNote.Trim().Length < 5)
        {
            TempData["ErrorMessage"] = "Từ chối hàng loạt cần có lý do rõ ràng.";
            return RedirectToAction(nameof(PoiPending), "Approval", new { area = "Reviewer" });
        }

        var reason = adminNote.Trim();
        var pois = await _context.Pois
            .Where(poi => selectedIds.Contains(poi.Id) && poi.Status == "Pending")
            .ToListAsync(cancellationToken);

        foreach (var poi in pois)
        {
            poi.Status = "Rejected";
            poi.AdminNote = reason;
            await SyncOwnerRequestsAfterPoiReviewAsync(poi.Id, "Rejected", reason, cancellationToken);
            await LogAsync("ReviewerBulkRejectPoi", "pois", poi.Id, $"Reviewer từ chối nhanh POI #{poi.Id}: {reason}", cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Đã từ chối {pois.Count} POI.";
        return RedirectToAction(nameof(PoiPending), "Approval", new { area = "Reviewer" });
    }

    private async Task SyncOwnerRequestsAfterPoiReviewAsync(int poiId, string status, string? note, CancellationToken cancellationToken)
    {
        var requests = await _context.PoiOwnerRequests
            .Where(item => item.PoiId == poiId && item.Status == "Pending")
            .ToListAsync(cancellationToken);

        if (requests.Count == 0)
            return;

        var reviewedAt = DateTime.UtcNow;
        foreach (var request in requests)
        {
            request.Status = status;
            request.ReviewedAt = reviewedAt;

            if (status == "Rejected" && !string.IsNullOrWhiteSpace(note))
            {
                request.Note = string.IsNullOrWhiteSpace(request.Note)
                    ? note.Trim()
                    : $"{request.Note}{System.Environment.NewLine}Admin/Reviewer: {note.Trim()}";
            }
        }
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

    private static ReviewerPoiReviewItemViewModel ToReviewItem(Poi poi, IReadOnlyList<string> activeLanguages)
    {
        var translations = poi.Translations ?? [];
        var translationCodes = translations.Select(item => item.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingLanguages = activeLanguages.Where(code => !translationCodes.Contains(code)).ToList();
        var vi = translations.FirstOrDefault(item => item.LanguageCode == "vi") ?? translations.FirstOrDefault();
        var missingAudio = translations.Count(item => string.IsNullOrWhiteSpace(item.AudioUrl));
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
        if (!hasImage)
            issues.Add("Thiếu ảnh/media");
        if (poi.PoiCategories.Count == 0)
            issues.Add("Chưa gắn danh mục");
        if (string.IsNullOrWhiteSpace(poi.QrCodeToken))
            issues.Add("Thiếu QR");
        if (poi.Latitude == 0 || poi.Longitude == 0)
            issues.Add("Tọa độ chưa hợp lệ");

        var score = 100;
        score -= missingLanguages.Count * 8;
        score -= missingAudio * 5;
        score -= string.IsNullOrWhiteSpace(vi?.ShortDescription) ? 10 : 0;
        score -= string.IsNullOrWhiteSpace(vi?.FullDescription) ? 15 : 0;
        score -= hasImage ? 0 : 12;
        score -= poi.PoiCategories.Count > 0 ? 0 : 8;
        score -= string.IsNullOrWhiteSpace(poi.QrCodeToken) ? 10 : 0;
        score = Math.Clamp(score, 0, 100);

        return new ReviewerPoiReviewItemViewModel
        {
            Id = poi.Id,
            Name = vi?.Name ?? $"POI #{poi.Id}",
            Status = poi.Status,
            AdminNote = poi.AdminNote,
            CreatorName = poi.Creator?.Username,
            OwnerName = poi.OwnerProfile?.BusinessName,
            CreatedAt = poi.CreatedAt,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            TranslationCount = translations.Count,
            MissingLanguageCount = missingLanguages.Count,
            MissingAudioCount = missingAudio,
            QualityScore = score,
            Issues = issues
        };
    }

    private async Task<(int Pending, int Approved, int Rejected)> BuildCountsAsync(CancellationToken cancellationToken)
    {
        var statuses = await _context.Pois
            .AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return (
            statuses.Where(item => item.Status == "Pending").Sum(item => item.Count),
            statuses.Where(item => item.Status == "Approved").Sum(item => item.Count),
            statuses.Where(item => item.Status == "Rejected").Sum(item => item.Count));
    }

    private async Task LogAsync(string action, string targetTable, int targetId, string description, CancellationToken cancellationToken)
    {
        var reviewerId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : (int?)null;

        _context.AdminActivityLogs.Add(new AdminActivityLog
        {
            UserId = reviewerId,
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
