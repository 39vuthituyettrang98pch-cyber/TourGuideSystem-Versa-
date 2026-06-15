using System.Security.Claims;
using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers.Api;

[Route("api/user")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class UserApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<TouristProfileDto>>> GetMe(
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var profile = await _context.Tourists
            .AsNoTracking()
            .Where(tourist => tourist.Id == touristId)
            .Select(tourist => new TouristProfileDto
            {
                Id = tourist.Id,
                Email = tourist.Email ?? "",
                FullName = tourist.FullName ?? "",
                CreatedAt = tourist.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return profile is null
            ? NotFound(ApiResponse<TouristProfileDto>.Fail("Tài khoản không tồn tại."))
            : Ok(ApiResponse<TouristProfileDto>.Ok(profile));
    }

    private int GetTouristId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
