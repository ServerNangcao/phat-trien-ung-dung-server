namespace Orders.Internal.Domain;

internal static class RefundStatus
{
    public const string Pending = nameof(Pending);
    public const string Approved = nameof(Approved);
    public const string Rejected = nameof(Rejected);
    public const string Refunded = nameof(Refunded);
}

