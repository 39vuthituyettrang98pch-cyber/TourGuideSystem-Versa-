using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Controllers.Api;

[Route("api/auth")]
[ApiController]
public sealed class AuthApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly JwtTokenService _tokenService;

    public AuthApiController(
        AppDbContext context,
        PasswordService passwordService,
        JwtTokenService tokenService)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthDto>>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponse<AuthDto>.Fail("Họ tên, email và mật khẩu là bắt buộc."));
        }

        if (request.Password.Length < 8)
            return BadRequest(ApiResponse<AuthDto>.Fail("Mật khẩu phải có ít nhất 8 ký tự."));

        if (await _context.Tourists.AnyAsync(tourist => tourist.Email == email, cancellationToken))
            return BadRequest(ApiResponse<AuthDto>.Fail("Email này đã được sử dụng."));

        var tourist = new Tourist
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordService.Hash(request.Password),
            AuthProvider = "local",
            CreatedAt = DateTime.UtcNow
        };

        _context.Tourists.Add(tourist);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<AuthDto>.Ok(
            _tokenService.Create(tourist),
            "Đăng ký thành công."));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthDto>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(ApiResponse<AuthDto>.Fail("Email và mật khẩu là bắt buộc."));

        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Email == email, cancellationToken);

        if (tourist == null ||
            !_passwordService.Verify(request.Password, tourist.PasswordHash, out var needsUpgrade))
        {
            return BadRequest(ApiResponse<AuthDto>.Fail("Email hoặc mật khẩu không chính xác."));
        }

        if (needsUpgrade)
        {
            tourist.PasswordHash = _passwordService.Hash(request.Password);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResponse<AuthDto>.Ok(
            _tokenService.Create(tourist),
            "Đăng nhập thành công."));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<TouristProfileDto>>> GetMe(
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var tourist = await _context.Tourists
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);

        if (tourist == null)
            return NotFound(ApiResponse<TouristProfileDto>.Fail("Tài khoản không tồn tại."));

        return Ok(ApiResponse<TouristProfileDto>.Ok(new TouristProfileDto
        {
            Id = tourist.Id,
            Email = tourist.Email ?? "",
            FullName = tourist.FullName ?? "",
            CreatedAt = tourist.CreatedAt
        }));
    }

    private int GetTouristId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}

public sealed class RegisterRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string FullName { get; set; } = "";
}

public sealed class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
