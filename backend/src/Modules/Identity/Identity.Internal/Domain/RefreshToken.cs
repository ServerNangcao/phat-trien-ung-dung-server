using Ticketing.SharedKernel;

namespace Identity.Internal.Domain;

internal sealed class RefreshToken : BaseEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAt)
    {
        return new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Revoke(Guid? replacedByTokenId = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
        UpdatedAt = DateTime.UtcNow;
    }
}
