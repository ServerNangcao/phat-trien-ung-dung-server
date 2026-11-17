using FluentAssertions;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Tests.Orders.Domain;

public class DiscountCodeTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_NormalizesCodeAndAcceptsValidPercentage()
    {
        var code = DiscountCode.Create("  summer26 ", DiscountCode.DiscountType.Percentage, 10m, null, null).Value;

        code.Code.Should().Be("SUMMER26");
        code.Type.Should().Be(DiscountCode.DiscountType.Percentage);
        code.IsActive.Should().BeTrue();
        code.TimesUsed.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCode_Fails(string code) =>
        DiscountCode.Create(code, DiscountCode.DiscountType.Percentage, 10m, null, null).IsFailure.Should().BeTrue();

    [Fact]
    public void Create_WithPercentageOutsideZeroToHundred_Fails() =>
        DiscountCode.Create("X", DiscountCode.DiscountType.Percentage, 150m, null, null).IsFailure.Should().BeTrue();

    [Fact]
    public void Create_WithFixedZeroOrNegative_Fails() =>
        DiscountCode.Create("X", DiscountCode.DiscountType.FixedVnd, 0m, null, null).IsFailure.Should().BeTrue();

    [Fact]
    public void Create_WithMaxRedemptionsBelowOne_Fails() =>
        DiscountCode.Create("X", DiscountCode.DiscountType.Percentage, 10m, 0, null).IsFailure.Should().BeTrue();

    [Fact]
    public void ComputeDiscount_Percentage_RoundsToWholeVnd()
    {
        var code = DiscountCode.Create("P15", DiscountCode.DiscountType.Percentage, 15m, null, null).Value;

        // 15% of 377,000 = 56,550 exactly; of 12,345 = 1,851.75 → 1,852 (away from zero).
        code.ComputeDiscount(377_000m, Now).Value.Should().Be(56_550m);
        code.ComputeDiscount(12_345m, Now).Value.Should().Be(1_852m);
    }

    [Fact]
    public void ComputeDiscount_Fixed_CapsAtItemsTotal()
    {
        var code = DiscountCode.Create("BIG", DiscountCode.DiscountType.FixedVnd, 100_000m, null, null).Value;

        code.ComputeDiscount(50_000m, Now).Value.Should().Be(50_000m);
        code.ComputeDiscount(300_000m, Now).Value.Should().Be(100_000m);
    }

    [Fact]
    public void ComputeDiscount_Expired_Fails()
    {
        var code = DiscountCode.Create("OLD", DiscountCode.DiscountType.Percentage, 10m, null, Now.AddMinutes(-1)).Value;

        code.ComputeDiscount(300_000m, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ComputeDiscount_Exhausted_FailsAfterMaxRedemptions()
    {
        var code = DiscountCode.Create("ONCE", DiscountCode.DiscountType.Percentage, 10m, 1, null).Value;

        code.ComputeDiscount(300_000m, Now).IsSuccess.Should().BeTrue();
        code.RecordRedemption();
        code.ComputeDiscount(300_000m, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ComputeDiscount_Retired_Fails()
    {
        var code = DiscountCode.Create("GONE", DiscountCode.DiscountType.Percentage, 10m, null, null).Value;
        code.Retire();

        code.ComputeDiscount(300_000m, Now).IsFailure.Should().BeTrue();
    }
}
