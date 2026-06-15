using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Controllers.Api;

[Route("api/favorite")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class FavoriteApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public FavoriteApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<int>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var poiIds = await _context.TouristFavorites
            .AsNoTracking()
            .Where(item => item.TouristId == touristId && item.TargetType == "POI")
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.TargetId)
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<int>>.Ok(poiIds));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<bool>>> SetFavorite(
        [FromBody] FavoriteRequest request,
        CancellationToken cancellationToken)
    {
        var poiExists = await _context.Pois
            .AnyAsync(item => item.Id == request.PoiId && item.Status == "Approved", cancellationToken);
        if (!poiExists)
            return BadRequest(ApiResponse<bool>.Fail("POI không tồn tại hoặc chưa được duyệt."));

        var touristId = GetTouristId();
        var favorite = await _context.TouristFavorites.FirstOrDefaultAsync(
            item => item.TouristId == touristId &&
                    item.TargetType == "POI" &&
                    item.TargetId == request.PoiId,
            cancellationToken);

        if (request.IsFavorite && favorite == null)
        {
            _context.TouristFavorites.Add(new TouristFavorite
            {
                TouristId = touristId,
                TargetType = "POI",
                TargetId = request.PoiId,
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (!request.IsFavorite && favorite != null)
        {
            _context.TouristFavorites.Remove(favorite);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<bool>.Ok(request.IsFavorite));
    }

    private int GetTouristId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
