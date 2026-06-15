using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Controllers.Api;

[Route("api/recent")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RecentApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public RecentApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<int>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var poiIds = await _context.VisitorPlaybackLogs
            .AsNoTracking()
            .Where(item => item.TouristId == touristId)
            .GroupBy(item => item.PoiId)
            .Select(group => new
            {
                PoiId = group.Key,
                LastOpened = group.Max(item => item.CreatedAt)
            })
            .OrderByDescending(item => item.LastOpened)
            .Take(30)
            .Select(item => item.PoiId)
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<int>>.Ok(poiIds));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<long>>> Add(
        [FromBody] RecentRequest request,
        CancellationToken cancellationToken)
    {
        var poiExists = await _context.Pois
            .AnyAsync(item => item.Id == request.PoiId && item.Status == "Approved", cancellationToken);
        if (!poiExists)
            return BadRequest(ApiResponse<long>.Fail("POI không tồn tại hoặc chưa được duyệt."));

        var touristId = GetTouristId();
        var log = new VisitorPlaybackLog
        {
            TouristId = touristId,
            DeviceId = $"tourist-{touristId}",
            PoiId = request.PoiId,
            LanguageCode = "vi",
            TriggerType = "Open",
            ListenDuration = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.VisitorPlaybackLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<long>.Ok(log.Id));
    }

    private int GetTouristId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
