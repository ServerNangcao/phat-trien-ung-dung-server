using System;
using Ticketing.SharedKernel;
using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Notifications.Domain.Entities;

/// <summary>
/// Represents a record of an email that was sent (or attempted) by the system.
/// Lifecycle: Pending → Sent, or Pending → Failed (with scheduled retries) → Sent.
/// </summary>
public sealed class EmailHistory : Entity
{
    /// <summary>
    /// Order ID associated with this email (for tracking).
    /// </summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Recipient email address.
    /// </summary>
    public string To { get; private set; }

    /// <summary>
    /// Subject line that was sent.
    /// </summary>
    public string Subject { get; private set; }

    /// <summary>
    /// Body content that was sent.
    /// </summary>
    public string Body { get; private set; }

    /// <summary>
    /// Timestamp when the email was sent. Null when not yet sent.
    /// </summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>
    /// Timestamp when the email record was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Current status of the email.
    /// </summary>
    public EmailStatus Status { get; private set; }

    /// <summary>
    /// Number of send attempts performed.
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// Earliest time the next retry may run. Null when no retry is scheduled.
    /// </summary>
    public DateTime? NextRetryAt { get; private set; }

    /// <summary>
    /// The error from the most recent failed attempt. Null until a failure occurs.
    /// </summary>
    public string? LastError { get; private set; }

    private EmailHistory(
        Guid id,
        Guid orderId,
        string to,
        string subject,
        string body,
        DateTime? sentAt,
        EmailStatus status,
        int retryCount,
        DateTime createdAt) : base(id)
    {
        OrderId = orderId;
        To = to;
        Subject = subject;
        Body = body;
        SentAt = sentAt;
        Status = status;
        RetryCount = retryCount;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Creates a new <see cref="EmailHistory"/> instance with validation.
    /// </summary>
    public static Result<EmailHistory> Create(Guid orderId, string to, string subject, string body, DateTime createdAt)
    {
        if (orderId == Guid.Empty)
            return Result<EmailHistory>.Failure(Error.Validation("Order ID is required."));

        if (string.IsNullOrWhiteSpace(to))
            return Result<EmailHistory>.Failure(Error.Validation("Recipient address ('To') is required."));

        if (!to.Contains('@'))
            return Result<EmailHistory>.Failure(Error.Validation("Recipient address is not a valid email."));

        if (string.IsNullOrWhiteSpace(subject))
            return Result<EmailHistory>.Failure(Error.Validation("Subject is required."));

        if (string.IsNullOrWhiteSpace(body))
            return Result<EmailHistory>.Failure(Error.Validation("Body is required."));

        var email = new EmailHistory(
            Guid.NewGuid(),
            orderId,
            to.Trim(),
            subject.Trim(),
            body,
            null,
            EmailStatus.Pending,
            0,
            createdAt);

        return Result<EmailHistory>.Success(email);
    }

    /// <summary>
    /// Marks the email as sent.
    /// </summary>
    public void MarkSent(DateTime sentAt)
    {
        SentAt = sentAt;
        Status = EmailStatus.Sent;
        NextRetryAt = null;
    }

    /// <summary>
    /// Marks the email as failed and increments the attempt counter.
    /// </summary>
    public void MarkFailed(string? error = null)
    {
        RetryCount++;
        Status = EmailStatus.Failed;
        LastError = error;
    }

    /// <summary>
    /// Schedules the next retry; the email stays Failed until a later attempt succeeds.
    /// </summary>
    public void ScheduleRetry(DateTime nextRetryAt)
    {
        NextRetryAt = nextRetryAt;
    }
}

/// <summary>
/// Represents the lifecycle status of an email.
/// </summary>
public enum EmailStatus
{
    /// <summary>Not yet processed.</summary>
    Pending,

    /// <summary>Successfully sent.</summary>
    Sent,

    /// <summary>Failed to send (awaiting retry or exhausted).</summary>
    Failed
}
