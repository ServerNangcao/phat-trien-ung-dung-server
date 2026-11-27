using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Identity.Internal.Domain;
using Identity.Internal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ticketing.SharedKernel;

namespace Identity.Internal.Application.Services;

internal sealed record IssueOtpResult(
    Guid? OtpId,
    string? OtpCode,
    int CooldownSeconds,
    bool IsThrottled);

internal sealed record VerifyOtpResult(
    bool IsValid,
    Guid? OtpId,
    string? ErrorMessage);

/// <summary>
/// Quản lý phát hành, băm SHA-256, chống gửi lại quá nhanh và xác thực mã OTP 6 chữ số.
/// </summary>
internal sealed class PasswordResetOtpService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IClock _clock;
    private readonly PasswordResetOptions _options;

    public PasswordResetOtpService(
        IdentityDbContext dbContext,
        IClock clock,
        IOptions<PasswordResetOptions> options)
    {
        _dbContext = dbContext;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<IssueOtpResult> IssueOtpAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var cooldown = Math.Max(0, _options.ResendCooldownSeconds);
        var expirationMinutes = Math.Clamp(_options.ExpirationMinutes, 1, 60);
        var maxAttempts = Math.Clamp(_options.MaxAttempts, 1, 10);

        var latest = await _dbContext.PasswordResetOtps
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && !latest.IsUsed && cooldown > 0)
        {
            var elapsedSeconds = (now - latest.CreatedAt).TotalSeconds;
            if (elapsedSeconds < cooldown)
            {
                var remaining = (int)Math.Ceiling(cooldown - elapsedSeconds);
                return new IssueOtpResult(null, null, Math.Max(1, remaining), IsThrottled: true);
            }
        }

        // Vô hiệu hóa tất cả mã OTP cũ chưa sử dụng của người dùng
        var activeOtps = await _dbContext.PasswordResetOtps
            .Where(x => x.UserId == user.Id && x.UsedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var activeOtp in activeOtps)
        {
            activeOtp.MarkUsed(now);
        }

        var normalizedEmail = NormalizeEmail(user.Email!);
        var otpCode = RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
        var codeHash = HashCode(normalizedEmail, otpCode);

        var entity = PasswordResetOtp.Create(
            user.Id,
            normalizedEmail,
            codeHash,
            now,
            TimeSpan.FromMinutes(expirationMinutes),
            maxAttempts);

        _dbContext.PasswordResetOtps.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IssueOtpResult(entity.Id, otpCode, cooldown, IsThrottled: false);
    }

    public async Task RevokeOtpAsync(Guid otpId, CancellationToken cancellationToken = default)
    {
        var otp = await _dbContext.PasswordResetOtps
            .FirstOrDefaultAsync(x => x.Id == otpId, cancellationToken);

        if (otp is null)
        {
            return;
        }

        // Xóa OTP chưa gửi được email để người dùng có thể gửi lại ngay mà không bị kẹt cooldown
        _dbContext.PasswordResetOtps.Remove(otp);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(
        ApplicationUser user,
        string rawOtpCode,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var trimmed = rawOtpCode.Trim();

        var latest = await _dbContext.PasswordResetOtps
            .Where(x => x.UserId == user.Id && x.UsedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null || latest.IsExpired(now))
        {
            return new VerifyOtpResult(
                false,
                null,
                "Mã xác nhận không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu mã mới.");
        }

        if (latest.IsExhausted)
        {
            return new VerifyOtpResult(
                false,
                null,
                "Bạn đã nhập sai mã quá nhiều lần. Vui lòng yêu cầu mã xác nhận mới.");
        }

        var expectedBytes = Encoding.UTF8.GetBytes(latest.CodeHash);
        var actualBytes = Encoding.UTF8.GetBytes(HashCode(latest.NormalizedEmail, trimmed));
        var isMatch = expectedBytes.Length == actualBytes.Length &&
                      CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);

        if (!isMatch)
        {
            latest.RecordFailedAttempt(now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var message = latest.IsExhausted
                ? "Bạn đã nhập sai mã quá nhiều lần. Vui lòng yêu cầu mã xác nhận mới."
                : "Mã xác nhận không chính xác.";

            return new VerifyOtpResult(false, null, message);
        }

        return new VerifyOtpResult(true, latest.Id, null);
    }

    public async Task MarkOtpUsedAsync(Guid otpId, CancellationToken cancellationToken = default)
    {
        var otp = await _dbContext.PasswordResetOtps
            .FirstOrDefaultAsync(x => x.Id == otpId, cancellationToken);

        if (otp is null || otp.IsUsed)
        {
            return;
        }

        otp.MarkUsed(_clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static string NormalizeEmail(string email) =>
        email.Trim().ToUpperInvariant();

    internal static string HashCode(string normalizedEmail, string otpCode)
    {
        var payload = $"{normalizedEmail}:{otpCode}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
