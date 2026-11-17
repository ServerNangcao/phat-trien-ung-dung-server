using FluentAssertions;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Tests.Orders.Domain;

public class DiscountCodeEdgeCaseTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_CodeExceedsMaxLength_Fails()
    {
        var longCode = new string('X', 51);

        var result = DiscountCode.Create(longCode, DiscountCode.DiscountType.Percentage, 10m, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("50 characters");
    }

    [Fact]
    public void Create_CodeAtMaxLength_Succeeds()
    {
        var maxCode = new string('X', 50);

        var result = DiscountCode.Create(maxCode, DiscountCode.DiscountType.Percentage, 10m, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be(maxCode);
    }

    [Fact]
    public void ComputeDiscount_Percentage_RoundsAwayFromZero()
    {
        var code = DiscountCode.Create("P33", DiscountCode.DiscountType.Percentage, 33m, null, null).Value;

        // 33% of 100,000 = 33,000 exactly
        code.ComputeDiscount(100_000m, Now).Value.Should().Be(33_000m);
        // 33% of 100,001 = 33,000.33 → 33,000
        code.ComputeDiscount(100_001m, Now).Value.Should().Be(33_000m);
        // 33% of 100,002 = 33,000.66 → 33,001
        code.ComputeDiscount(100_002m, Now).Value.Should().Be(33_001m);
    }

    [Fact]
    public void ComputeDiscount_EmptyOrderTotal_Fails()
    {
        var code = DiscountCode.Create("TEST", DiscountCode.DiscountType.Percentage, 10m, null, null).Value;

        var result = code.ComputeDiscount(0m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("empty order");
    }

    [Fact]
    public void ComputeDiscount_NegativeOrderTotal_Fails()
    {
        var code = DiscountCode.Create("TEST", DiscountCode.DiscountType.Percentage, 10m, null, null).Value;

        var result = code.ComputeDiscount(-100m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
    }

    [Fact]
    public void RecordRedemption_IncrementsTimesUsed()
    {
        var code = DiscountCode.Create("COUNT", DiscountCode.DiscountType.Percentage, 10m, 5, null).Value;

        code.TimesUsed.Should().Be(0);
        code.RecordRedemption();
        code.TimesUsed.Should().Be(1);
        code.RecordRedemption();
        code.TimesUsed.Should().Be(2);
    }

    [Fact]
    public void Retire_SetsIsActiveFalseAndBlocksFutureUse()
    {
        var code = DiscountCode.Create("RETIRE", DiscountCode.DiscountType.Percentage, 10m, null, null).Value;

        code.IsActive.Should().BeTrue();
        code.Retire();
        code.IsActive.Should().BeFalse();

        var result = code.ComputeDiscount(100_000m, Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("no longer active");
    }

    [Fact]
    public void Create_FixedWithLargeValue_DoesNotCapAtCreation()
    {
        var code = DiscountCode.Create("HUGE", DiscountCode.DiscountType.FixedVnd, 1_000_000m, null, null).Value;

        code.Value.Should().Be(1_000_000m);
        // Cap only applies at ComputeDiscount time
        code.ComputeDiscount(500_000m, Now).Value.Should().Be(500_000m);
    }

    [Fact]
    public void ComputeDiscount_ExactlyAtMaxRedemptions_AllowsLastUse()
    {
        var code = DiscountCode.Create("LIMIT2", DiscountCode.DiscountType.Percentage, 10m, 2, null).Value;

        code.ComputeDiscount(100_000m, Now).IsSuccess.Should().BeTrue();
        code.RecordRedemption();
        code.ComputeDiscount(100_000m, Now).IsSuccess.Should().BeTrue();
        code.RecordRedemption();
        code.ComputeDiscount(100_000m, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_UnlimitedMaxRedemptions_NullIsAccepted()
    {
        var code = DiscountCode.Create("UNLIMITED", DiscountCode.DiscountType.Percentage, 10m, null, null).Value;

        code.MaxRedemptions.Should().BeNull();
        for (int i = 0; i < 100; i++)
        {
            code.ComputeDiscount(100_000m, Now).IsSuccess.Should().BeTrue();
            code.RecordRedemption();
        }
        code.TimesUsed.Should().Be(100);
    }
}