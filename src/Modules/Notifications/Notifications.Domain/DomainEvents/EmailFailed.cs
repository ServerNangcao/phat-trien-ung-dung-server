using System;
using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Notifications.Domain.DomainEvents;

/// <summary>
/// Raised when an email could not be sent.
/// </summary>
public sealed record EmailFailed(Guid EmailHistoryId, string Reason) : DomainEvent
{
    /// <summary>
    /// Identifier of the <see cref="Ticketing.Modules.Notifications.Domain.Entities.EmailHistory"/> that failed.
    /// </summary>
    public Guid EmailHistoryId { get; init; } = EmailHistoryId;

    /// <summary>
    /// Human‑readable reason for the failure.
    /// </summary>
    public string Reason { get; init; } = Reason;
}
