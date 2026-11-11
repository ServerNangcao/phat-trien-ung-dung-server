namespace Ticketing.Internal.Domain;

internal static class ReservationStatus
{
    public const string Active = nameof(Active);
    public const string PaymentPending = nameof(PaymentPending);
    public const string Confirmed = nameof(Confirmed);
    public const string Released = nameof(Released);
}

