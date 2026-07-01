using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly PasswordResetService _passwordResetService;

    public AuthApiController(
        AppDbContext context,
        PasswordService passwordService,
        JwtTokenService tokenService,
        PasswordResetService passwordResetService)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _passwordResetService = passwordResetService;
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

        if (!IsGmailAddress(email))
            return BadRequest(ApiResponse<AuthDto>.Fail("Chỉ chấp nhận đăng ký bằng Gmail có đuôi @gmail.com."));

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
    [EnableRateLimiting("PasswordResetPerIp")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var genericMessage =
            "Nếu email đã đăng ký, mã OTP 6 số đã được gửi. Mã có hiệu lực trong 10 phút.";

        if (string.IsNullOrWhiteSpace(email))
            return Ok(ApiResponse<object>.Ok(new { }, genericMessage));

        var result = await _passwordResetService.RequestOtpAsync(email, cancellationToken);
        object data = !string.IsNullOrWhiteSpace(result.DebugOtp)
            ? new { debugOtp = result.DebugOtp }
            : new { };

        return Ok(ApiResponse<object>.Ok(data, genericMessage));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [EnableRateLimiting("PasswordResetPerIp")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Otp) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < 8)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Email, OTP 6 số và mật khẩu mới ít nhất 8 ký tự là bắt buộc."));
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu xác nhận không khớp."));

        var status = await _passwordResetService.ResetPasswordAsync(
            request.Email,
            request.Otp,
            request.NewPassword,
            cancellationToken);

        if (status != PasswordResetStatus.Success)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Mã OTP không đúng, đã hết hạn hoặc đã được sử dụng."));
        }

        return Ok(ApiResponse<object>.Ok(
            new { },
            "Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới."));
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

        if (!IsGmailAddress(email))
            return BadRequest(ApiResponse<TouristProfileDto>.Fail("Chỉ chấp nhận địa chỉ Gmail có đuôi @gmail.com."));

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

    private static bool IsGmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var atIndex = email.IndexOf('@');
        return atIndex > 0 &&
               atIndex == email.LastIndexOf('@') &&
               email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

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

public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = "";
    public string Otp { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
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
