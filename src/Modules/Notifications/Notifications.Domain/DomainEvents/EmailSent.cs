using System;
using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Notifications.Domain.DomainEvents;

/// <summary>
/// Raised when an email has been successfully sent.
/// </summary>
public sealed record EmailSent(Guid EmailHistoryId) : DomainEvent
{
    /// <summary>
    /// Identifier of the <see cref="Ticketing.Modules.Notifications.Domain.Entities.EmailHistory"/> that was sent.
    /// </summary>
    public Guid EmailHistoryId { get; init; } = EmailHistoryId;
}
