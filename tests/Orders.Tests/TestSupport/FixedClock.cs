using Ticketing.SharedKernel;

namespace Ticketing.Tests.Orders.TestSupport;

/// <summary>Deterministic clock for tests.</summary>
public sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;

    public DateTimeOffset UtcNowOffset => new(UtcNow, TimeSpan.Zero);
}
