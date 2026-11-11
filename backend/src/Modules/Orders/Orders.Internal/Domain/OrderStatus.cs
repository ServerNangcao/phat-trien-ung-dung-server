namespace Orders.Internal.Domain;

internal static class OrderStatus
{
    public const string Pending = nameof(Pending);
    public const string Paid = nameof(Paid);
    public const string Expired = nameof(Expired);
    public const string Cancelled = nameof(Cancelled);
    public const string PaidAfterExpiry = nameof(PaidAfterExpiry);
    public const string Refunded = nameof(Refunded);
}

