using Ticketing.Modules.Orders.Domain.ValueObjects;
using Ticketing.SharedKernel;

namespace Ticketing.Tests.Orders.Domain;

public class OrderItemTests
{
    private static readonly Guid TicketTypeId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidInput_SucceedsAndComputesSubtotal()
    {
        var result = OrderItem.Create(TicketTypeId, 3, 100_000m, "VIP");

        result.IsSuccess.Should().BeTrue();
        var item = result.Value;
        item.TicketTypeId.Should().Be(TicketTypeId);
        item.Quantity.Should().Be(3);
        item.Price.Should().Be(100_000m);
        item.TicketTypeName.Should().Be("VIP");
        item.Subtotal.Should().Be(300_000m);
    }

    [Fact]
    public void Create_WithZeroQuantity_Fails()
    {
        var result = OrderItem.Create(TicketTypeId, 0, 100_000m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Quantity must be greater than zero");
    }

    [Fact]
    public void Create_WithNegativeQuantity_Fails()
    {
        var result = OrderItem.Create(TicketTypeId, -1, 100_000m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Quantity must be greater than zero");
    }

    [Fact]
    public void Create_WithNegativePrice_Fails()
    {
        var result = OrderItem.Create(TicketTypeId, 1, -100m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Price cannot be negative");
    }

    [Fact]
    public void Create_WithEmptyTicketTypeId_Fails()
    {
        var result = OrderItem.Create(Guid.Empty, 1, 100_000m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Ticket type identifier cannot be empty");
    }

    [Fact]
    public void Create_WithEmptyTicketTypeName_DefaultsToEmptyString()
    {
        var result = OrderItem.Create(TicketTypeId, 1, 100_000m, "");

        result.IsSuccess.Should().BeTrue();
        result.Value.TicketTypeName.Should().Be(string.Empty);
    }

    [Fact]
    public void Subtotal_CalculatesCorrectly_QuantityTimesPrice()
    {
        var item = OrderItem.Create(TicketTypeId, 5, 20_000m).Value;
        item.Subtotal.Should().Be(100_000m);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var item1 = OrderItem.Create(TicketTypeId, 2, 125_000m, "VIP").Value;
        var item2 = OrderItem.Create(TicketTypeId, 2, 125_000m, "VIP").Value;

        item1.Should().Be(item2);
        (item1 == item2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentQuantity_AreNotEqual()
    {
        var item1 = OrderItem.Create(TicketTypeId, 2, 125_000m, "VIP").Value;
        var item2 = OrderItem.Create(TicketTypeId, 3, 125_000m, "VIP").Value;

        item1.Should().NotBe(item2);
    }

    [Fact]
    public void Equality_DifferentPrice_AreNotEqual()
    {
        var item1 = OrderItem.Create(TicketTypeId, 2, 125_000m, "VIP").Value;
        var item2 = OrderItem.Create(TicketTypeId, 2, 100_000m, "VIP").Value;

        item1.Should().NotBe(item2);
    }

    [Fact]
    public void Equality_DifferentTicketTypeName_AreNotEqual()
    {
        var item1 = OrderItem.Create(TicketTypeId, 2, 125_000m, "VIP").Value;
        var item2 = OrderItem.Create(TicketTypeId, 2, 125_000m, "Standard").Value;

        item1.Should().NotBe(item2);
    }
}