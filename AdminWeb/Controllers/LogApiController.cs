using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers.Api;

[Route("api/log")]
[ApiController]
public sealed class LogApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public LogApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> PostLog(
        [FromBody] VisitorPlaybackLog log,
        CancellationToken cancellationToken)
    {
        if (log.PoiId <= 0 ||
            string.IsNullOrWhiteSpace(log.DeviceId) ||
            !await _context.Pois.AnyAsync(item => item.Id == log.PoiId, cancellationToken))
        {
            return BadRequest(new { success = false, message = "Dữ liệu lịch sử phát không hợp lệ.", data = (object?)null });
        }

        if (log.TouristId.HasValue &&
            !await _context.Tourists.AnyAsync(item => item.Id == log.TouristId.Value, cancellationToken))
        {
            return BadRequest(new { success = false, message = "Tài khoản du khách không tồn tại.", data = (object?)null });
        }

        log.Id = 0;
        log.DeviceId = log.DeviceId.Trim();
        log.LanguageCode = string.IsNullOrWhiteSpace(log.LanguageCode)
            ? "vi"
            : log.LanguageCode.Trim().ToLowerInvariant();
        log.ListenDuration = Math.Max(0, log.ListenDuration);
        log.CreatedAt = log.CreatedAt == default ? DateTime.UtcNow : log.CreatedAt;
        log.Poi = null;
        log.Tourist = null;

        _context.VisitorPlaybackLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, message = "Đã lưu lịch sử phát audio.", data = new { log.Id } });
    }
}
