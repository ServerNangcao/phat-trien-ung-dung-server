namespace Ticketing.Modules.Notifications.Contracts.IntegrationEvents;

/// <summary>
/// Inbound copy of the Orders module' event (TV2 publishes, TV2 consumes for the
/// customer email): payment arrived after the reservation expired (ADR-0003), so the
/// customer must be told the tickets cannot be issued and an automatic refund request
/// was created (UC-ORD-02 step 6). EventId drives Inbox dedupe.
/// </summary>
public sealed record OrderPaidAfterExpiryIntegrationEvent
{
    public Guid EventId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime PaidAt { get; init; }
    public string Reason { get; init; } = string.Empty;

    /// <summary>Empty when Orders did not learn the customer's email from the reservation.</summary>
    public string CustomerEmail { get; init; } = string.Empty;
}
