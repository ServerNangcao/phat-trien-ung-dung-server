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
    /// The subtotal for this line item (quantity multiplied by price).
    /// </summary>
    public decimal Subtotal => Quantity * Price;

    private OrderItem(Guid ticketTypeId, int quantity, decimal price)
    {
        TicketTypeId = ticketTypeId;
        Quantity = quantity;
        Price = price;
    }

    /// <summary>
    /// Create a new order item, validating the input values.
    /// </summary>
    /// <param name="ticketTypeId">The ticket type identifier.</param>
    /// <param name="quantity">The number of units to purchase.</param>
    /// <param name="price">The price per unit.</param>
    /// <returns>A result containing the item on success, or a validation error.</returns>
    public static Result<OrderItem> Create(Guid ticketTypeId, int quantity, decimal price)
    {
        if (ticketTypeId == Guid.Empty)
            return Result<OrderItem>.Failure(Error.Validation("Ticket type identifier cannot be empty."));

        if (quantity <= 0)
            return Result<OrderItem>.Failure(Error.Validation("Quantity must be greater than zero."));

        if (price < 0)
            return Result<OrderItem>.Failure(Error.Validation("Price cannot be negative."));

        return Result<OrderItem>.Success(new OrderItem(ticketTypeId, quantity, price));
    }
}
