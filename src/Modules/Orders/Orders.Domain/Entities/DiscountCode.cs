using Ticketing.SharedKernel;
using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Domain.Entities;

/// <summary>
/// A discount code redeemable at order creation (FR-16). Codes are stored normalized
/// (trimmed, upper-invariant) so lookups are case-insensitive, and redemption counting
/// is optimistic: an over-redeem is caught by the unique reservation index on the order
/// side, never by silent overrun.
/// </summary>
public sealed class DiscountCode : Entity
{
    /// <summary>How the code's Value is interpreted.</summary>
    public enum DiscountType
    {
        /// <summary>Value is a percentage of the items total (0–100).</summary>
        Percentage = 0,

        /// <summary>Value is a fixed VND amount off the items total.</summary>
        FixedVnd = 1
    }

    /// <summary>The normalized code customers type in.</summary>
    public string Code { get; private init; } = string.Empty;

    /// <summary>Whether Value is a percentage or a fixed VND amount.</summary>
    public DiscountType Type { get; private init; }

    /// <summary>The percentage (0–100) or the fixed VND amount, depending on Type.</summary>
    public decimal Value { get; private init; }

    /// <summary>Maximum total redemptions; null means unlimited.</summary>
    public int? MaxRedemptions { get; private init; }

    /// <summary>How many orders have redeemed this code so far.</summary>
    public int TimesUsed { get; private set; }

    /// <summary>The instant after which the code stops applying; null means never expires.</summary>
    public DateTime? ExpiresAtUtc { get; private init; }

    /// <summary>Organizers can retire a code without deleting its history.</summary>
    public bool IsActive { get; private set; }

    private DiscountCode() : base(Guid.Empty)
    {
    }

    private DiscountCode(Guid id, string code, DiscountType type, decimal value,
        int? maxRedemptions, DateTime? expiresAtUtc) : base(id)
    {
        Code = code;
        Type = type;
        Value = value;
        MaxRedemptions = maxRedemptions;
        ExpiresAtUtc = expiresAtUtc;
        TimesUsed = 0;
        IsActive = true;
    }

    /// <summary>
    /// Create a discount code, validating the input values.
    /// </summary>
    public static Result<DiscountCode> Create(string code, DiscountType type, decimal value,
        int? maxRedemptions, DateTime? expiresAtUtc)
    {
        var normalized = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length == 0)
            return Result<DiscountCode>.Failure(Error.Validation("Discount code cannot be empty."));

        if (normalized.Length > 50)
            return Result<DiscountCode>.Failure(Error.Validation("Discount code cannot exceed 50 characters."));

        if (type is DiscountType.Percentage && (value <= 0 || value > 100))
            return Result<DiscountCode>.Failure(Error.Validation("Percentage discount must be between 0 and 100."));

        if (type is DiscountType.FixedVnd && value <= 0)
            return Result<DiscountCode>.Failure(Error.Validation("Fixed discount must be greater than zero."));

        if (maxRedemptions is < 1)
            return Result<DiscountCode>.Failure(Error.Validation("Max redemptions must be at least 1."));

        return Result<DiscountCode>.Success(new DiscountCode(Guid.NewGuid(), normalized, type, value, maxRedemptions, expiresAtUtc));
    }

    /// <summary>
    /// Compute the discount amount for an items total, or fail when the code cannot be
    /// used (retired, expired, or exhausted). Percentage results round to whole VND.
    /// </summary>
    public Result<decimal> ComputeDiscount(decimal itemsTotal, DateTime now)
    {
        if (!IsActive)
            return Result<decimal>.Failure(Error.Validation($"Discount code {Code} is no longer active."));

        if (ExpiresAtUtc is { } expires && expires <= now)
            return Result<decimal>.Failure(Error.Validation($"Discount code {Code} has expired."));

        if (MaxRedemptions is { } max && TimesUsed >= max)
            return Result<decimal>.Failure(Error.Validation($"Discount code {Code} has reached its redemption limit."));

        if (itemsTotal <= 0)
            return Result<decimal>.Failure(Error.Validation("Discount cannot apply to an empty order."));

        var discount = Type switch
        {
            DiscountType.Percentage => decimal.Round(itemsTotal * Value / 100m, 0, MidpointRounding.AwayFromZero),
            DiscountType.FixedVnd => Value,
            _ => 0m
        };

        return Result<decimal>.Success(Math.Min(discount, itemsTotal));
    }

    /// <summary>Count one redemption. Called in the same commit that creates the order.</summary>
    public void RecordRedemption()
    {
        TimesUsed++;
    }

    /// <summary>Retire the code (organizer action); it no longer applies to new orders.</summary>
    public void Retire()
    {
        IsActive = false;
    }
}
