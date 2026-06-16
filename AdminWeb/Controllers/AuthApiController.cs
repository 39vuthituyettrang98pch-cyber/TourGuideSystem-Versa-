using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AdminWeb.Controllers.Api;

[Route("api/auth")]
[ApiController]
public sealed class AuthApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly JwtTokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _environment;

    public AuthApiController(
        AppDbContext context,
        PasswordService passwordService,
        JwtTokenService tokenService,
        IEmailSender emailSender,
        IWebHostEnvironment environment)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _environment = environment;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthDto>>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
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
        var email = NormalizeEmail(request.Email);
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

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var genericMessage = "Nếu email hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi đến hộp thư của bạn.";

        if (string.IsNullOrWhiteSpace(email))
            return Ok(ApiResponse<object>.Ok(new { }, genericMessage));

        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Email == email, cancellationToken);

        if (tourist == null || string.IsNullOrWhiteSpace(tourist.Email))
            return Ok(ApiResponse<object>.Ok(new { }, genericMessage));

        var token = CreateResetToken();
        var tokenHash = HashResetToken(token);

        var oldTokens = await _context.PasswordResetTokens
            .Where(item => item.TouristId == tourist.Id && item.UsedAt == null)
            .ToListAsync(cancellationToken);

        if (oldTokens.Count > 0)
            _context.PasswordResetTokens.RemoveRange(oldTokens);

        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            TouristId = tourist.Id,
            Email = tourist.Email,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });

        await _context.SaveChangesAsync(cancellationToken);

        var resetUrl = Url.ActionLink(
            "ResetPassword",
            "Account",
            new { area = "DuKhach", email = tourist.Email, token });

        if (!string.IsNullOrWhiteSpace(resetUrl) && _emailSender.IsConfigured)
        {
            var body = $"""
                <div style="font-family:Arial,sans-serif;line-height:1.6;color:#102033">
                    <h2>Đặt lại mật khẩu VERSA Travel</h2>
                    <p>Xin chào {System.Net.WebUtility.HtmlEncode(tourist.FullName ?? "du khách")},</p>
                    <p>Bấm nút bên dưới để tạo mật khẩu mới. Liên kết này hết hạn sau 30 phút.</p>
                    <p><a href="{System.Net.WebUtility.HtmlEncode(resetUrl)}" style="display:inline-block;padding:12px 18px;border-radius:12px;background:#34d399;color:#03131d;font-weight:700;text-decoration:none">Đặt lại mật khẩu</a></p>
                    <p>Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email.</p>
                </div>
                """;

            await _emailSender.SendAsync(tourist.Email, "Đặt lại mật khẩu VERSA Travel", body, cancellationToken);
        }

        object data = _environment.IsDevelopment() && !string.IsNullOrWhiteSpace(resetUrl)
            ? new { debugResetLink = resetUrl }
            : new { };

        return Ok(ApiResponse<object>.Ok(data, genericMessage));
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

        return Ok(ApiResponse<TouristProfileDto>.Ok(ToProfileDto(tourist)));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPut("me")]
    public async Task<ActionResult<ApiResponse<TouristProfileDto>>> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);

        if (tourist == null)
            return NotFound(ApiResponse<TouristProfileDto>.Fail("Tài khoản không tồn tại."));

        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(ApiResponse<TouristProfileDto>.Fail("Họ tên và email là bắt buộc."));

        var emailExists = await _context.Tourists.AnyAsync(
            item => item.Id != touristId && item.Email == email,
            cancellationToken);

        if (emailExists)
            return BadRequest(ApiResponse<TouristProfileDto>.Fail("Email này đã được tài khoản khác sử dụng."));

        tourist.FullName = request.FullName.Trim();
        tourist.Email = email;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<TouristProfileDto>.Ok(ToProfileDto(tourist), "Cập nhật hồ sơ thành công."));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < 8)
        {
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu mới phải có ít nhất 8 ký tự."));
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu xác nhận không khớp."));

        var touristId = GetTouristId();
        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);

        if (tourist == null)
            return NotFound(ApiResponse<object>.Fail("Tài khoản không tồn tại."));

        if (!_passwordService.Verify(request.CurrentPassword, tourist.PasswordHash, out _))
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu hiện tại không chính xác."));

        tourist.PasswordHash = _passwordService.Hash(request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { }, "Đổi mật khẩu thành công."));
    }

    private int GetTouristId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private static TouristProfileDto ToProfileDto(Tourist tourist) =>
        new()
        {
            Id = tourist.Id,
            Email = tourist.Email ?? "",
            FullName = tourist.FullName ?? "",
            CreatedAt = tourist.CreatedAt
        };

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string CreateResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string HashResetToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
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

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = "";
}

public sealed class UpdateProfileRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}
