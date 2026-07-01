using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AdminWeb.Services;

public enum PasswordResetStatus
{
    Success,
    InvalidOrExpired
}

public sealed record PasswordResetOtpRequestResult(string? DebugOtp = null);

public sealed class PasswordResetService
{
    public const int OtpLength = 6;
    public const int OtpLifetimeMinutes = 10;

    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        AppDbContext context,
        PasswordService passwordService,
        IEmailSender emailSender,
        IWebHostEnvironment environment,
        ILogger<PasswordResetService> logger)
    {
        _context = context;
        _passwordService = passwordService;
        _emailSender = emailSender;
        _environment = environment;
        _logger = logger;
    }

    public async Task<PasswordResetOtpRequestResult> RequestOtpAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        // Luôn trả cùng một kiểu kết quả để không tiết lộ email nào đã đăng ký.
        if (tourist == null || string.IsNullOrWhiteSpace(tourist.Email))
            return new PasswordResetOtpRequestResult();

        var now = DateTime.UtcNow;
        var recentTokenExists = await _context.PasswordResetTokens.AnyAsync(
            item => item.TouristId == tourist.Id &&
                    item.UsedAt == null &&
                    item.ExpiresAt > now &&
                    item.CreatedAt > now - ResendCooldown,
            cancellationToken);

        if (recentTokenExists && !ShouldExposeDevelopmentOtp())
            return new PasswordResetOtpRequestResult();

        var otp = RandomNumberGenerator
            .GetInt32(100_000, 1_000_000)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

        var oldTokens = await _context.PasswordResetTokens
            .Where(item => item.TouristId == tourist.Id && item.UsedAt == null)
            .ToListAsync(cancellationToken);

        if (oldTokens.Count > 0)
            _context.PasswordResetTokens.RemoveRange(oldTokens);

        var resetToken = new PasswordResetToken
        {
            TouristId = tourist.Id,
            Email = normalizedEmail,
            TokenHash = HashOtp(normalizedEmail, otp),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(OtpLifetimeMinutes)
        };

        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync(cancellationToken);

        if (!_emailSender.IsConfigured)
        {
            _logger.LogWarning(
                "SMTP is not configured. Password reset OTP for {Email} was not sent.",
                normalizedEmail);

            return new PasswordResetOtpRequestResult(
                ShouldExposeDevelopmentOtp() ? otp : null);
        }

        try
        {
            await _emailSender.SendAsync(
                normalizedEmail,
                $"{otp} là mã OTP đặt lại mật khẩu VERSA Travel",
                BuildOtpEmail(tourist.FullName, otp),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Could not send password reset OTP to {Email}.",
                normalizedEmail);

            _context.PasswordResetTokens.Remove(resetToken);
            await _context.SaveChangesAsync(cancellationToken);

            return new PasswordResetOtpRequestResult();
        }

        return new PasswordResetOtpRequestResult();
    }

    public async Task<PasswordResetStatus> ResetPasswordAsync(
        string email,
        string otp,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedOtp = NormalizeOtp(otp);
        if (normalizedOtp.Length != OtpLength || normalizedOtp.Any(character => !char.IsDigit(character)))
            return PasswordResetStatus.InvalidOrExpired;

        var now = DateTime.UtcNow;
        var resetToken = await _context.PasswordResetTokens
            .AsNoTracking()
            .Where(item =>
                item.Email == normalizedEmail &&
                item.UsedAt == null &&
                item.ExpiresAt > now)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (resetToken == null || !OtpMatches(resetToken.TokenHash, normalizedEmail, normalizedOtp))
            return PasswordResetStatus.InvalidOrExpired;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Chỉ một yêu cầu đồng thời được quyền sử dụng mã OTP này.
        var claimedRows = await _context.PasswordResetTokens
            .Where(item =>
                item.Id == resetToken.Id &&
                item.UsedAt == null &&
                item.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.UsedAt, now),
                cancellationToken);

        if (claimedRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PasswordResetStatus.InvalidOrExpired;
        }

        var tourist = await _context.Tourists
            .FirstOrDefaultAsync(item => item.Id == resetToken.TouristId, cancellationToken);

        if (tourist == null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PasswordResetStatus.InvalidOrExpired;
        }

        tourist.PasswordHash = _passwordService.Hash(newPassword);

        var otherTokens = await _context.PasswordResetTokens
            .Where(item => item.TouristId == tourist.Id && item.Id != resetToken.Id)
            .ToListAsync(cancellationToken);

        if (otherTokens.Count > 0)
            _context.PasswordResetTokens.RemoveRange(otherTokens);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return PasswordResetStatus.Success;
    }

    public static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private bool ShouldExposeDevelopmentOtp() =>
        _environment.IsDevelopment() && !_emailSender.IsConfigured;

    private static string NormalizeOtp(string? otp) =>
        new((otp ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string HashOtp(string normalizedEmail, string otp)
    {
        var value = $"{normalizedEmail}\n{otp}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static bool OtpMatches(string storedHash, string normalizedEmail, string otp)
    {
        var candidateHash = HashOtp(normalizedEmail, otp);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(storedHash),
            Encoding.ASCII.GetBytes(candidateHash));
    }

    private static string BuildOtpEmail(string? fullName, string otp)
    {
        var safeName = System.Net.WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(fullName) ? "du khách" : fullName.Trim());

        return $"""
            <div style="font-family:Arial,sans-serif;line-height:1.6;color:#102033;max-width:560px;margin:auto">
                <h2>Đặt lại mật khẩu VERSA Travel</h2>
                <p>Xin chào {safeName},</p>
                <p>Mã OTP đặt lại mật khẩu của bạn là:</p>
                <div style="margin:24px 0;padding:18px;border-radius:16px;background:#ecfdf5;color:#065f46;font-size:32px;font-weight:800;letter-spacing:10px;text-align:center">
                    {otp}
                </div>
                <p>Mã chỉ có hiệu lực trong {OtpLifetimeMinutes} phút và chỉ dùng được một lần.</p>
                <p>Không chia sẻ mã này. Nếu bạn không yêu cầu đổi mật khẩu, hãy bỏ qua email.</p>
            </div>
            """;
    }
}
