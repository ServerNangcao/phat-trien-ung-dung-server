using Ticketing.SharedKernel;

namespace Identity.Internal.Domain;

/// <summary>
/// Lưu mã OTP 6 chữ số đã hash để đặt lại mật khẩu.
/// Không bao giờ lưu mã OTP dạng plaintext.
/// </summary>
internal sealed class PasswordResetOtp : BaseEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTime? UsedAt { get; private set; }

    public bool IsUsed => UsedAt.HasValue;
    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;
    public bool IsExhausted => AttemptCount >= MaxAttempts;
    public bool CanVerify(DateTime utcNow) => !IsUsed && !IsExpired(utcNow) && !IsExhausted;

    private PasswordResetOtp() { }

    public static PasswordResetOtp Create(
        Guid userId,
        string normalizedEmail,
        string codeHash,
        DateTime utcNow,
        TimeSpan ttl,
        int maxAttempts = 5)
    {
        return new PasswordResetOtp
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            NormalizedEmail = normalizedEmail,
            CodeHash = codeHash,
            CreatedAt = utcNow,
            ExpiresAt = utcNow.Add(ttl),
            AttemptCount = 0,
            MaxAttempts = maxAttempts
        };
    }

    public void RecordFailedAttempt(DateTime utcNow)
    {
        AttemptCount++;
        UpdatedAt = utcNow;
    }

    public void MarkUsed(DateTime utcNow)
    {
        UsedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
