namespace Ticketing.Modules.Notifications.Contracts.IntegrationEvents;

/// <summary>
/// Inbound copy of the Orders module's event (TV2 publishes, TV2 consumes for the
/// customer email): a refund request was approved and the money is on its way back
/// ("Thông báo tiến trình hoàn tiền", UC-ORD-02). EventId drives Inbox dedupe.
/// </summary>
public sealed record RefundProcessedIntegrationEvent
{
    public Guid EventId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime RefundedAt { get; init; }

    /// <summary>Empty when Orders did not learn the customer's email from the reservation.</summary>
    public string CustomerEmail { get; init; } = string.Empty;
}
