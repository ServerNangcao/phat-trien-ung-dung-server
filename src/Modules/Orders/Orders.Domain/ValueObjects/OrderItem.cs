using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Domain.ValueObjects;

/// <summary>
/// Value object representing a single line item in an order.
/// Equality is based on all property values, not identity.
/// </summary>
public sealed record OrderItem
{
    /// <summary>
    /// The identifier of the ticket type purchased.
    /// </summary>
    public Guid TicketTypeId { get; init; }

    /// <summary>
    /// The number of units of this ticket type.
    /// </summary>
    public int Quantity { get; init; }

    /// <summary>
    /// The unit price at the time of order creation.
    /// </summary>
    public decimal Price { get; init; }

    /// <summary>
    /// Display name of the ticket type, denormalized from the reservation so sales
    /// reports (FR-23) render labels without calling back into Ticketing.
    /// </summary>
    public string TicketTypeName { get; init; } = string.Empty;

    /// <summary>
    /// The subtotal for this line item (quantity multiplied by price).
    /// </summary>
    public decimal Subtotal => Quantity * Price;

    private OrderItem(Guid ticketTypeId, int quantity, decimal price, string ticketTypeName)
    {
        TicketTypeId = ticketTypeId;
        Quantity = quantity;
        Price = price;
        TicketTypeName = ticketTypeName;
    }

    /// <summary>
    /// Create a new order item, validating the input values.
    /// </summary>
    /// <param name="ticketTypeId">The ticket type identifier.</param>
    /// <param name="quantity">The number of units to purchase.</param>
    /// <param name="price">The price per unit.</param>
    /// <param name="ticketTypeName">The display name of the ticket type (may be empty).</param>
    /// <returns>A result containing the item on success, or a validation error.</returns>
    public static Result<OrderItem> Create(Guid ticketTypeId, int quantity, decimal price, string ticketTypeName = "")
    {
        if (ticketTypeId == Guid.Empty)
            return Result<OrderItem>.Failure(Error.Validation("Ticket type identifier cannot be empty."));

        if (quantity <= 0)
            return Result<OrderItem>.Failure(Error.Validation("Quantity must be greater than zero."));

        if (price < 0)
            return Result<OrderItem>.Failure(Error.Validation("Price cannot be negative."));

        return Result<OrderItem>.Success(new OrderItem(ticketTypeId, quantity, price, ticketTypeName ?? string.Empty));
    }
}
