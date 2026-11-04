using System;
using Ticketing.SharedKernel.Domain;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Domain.Entities;

/// <summary>
/// Represents a record of an email that was sent (or attempted) by the system.
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
    /// Number of retry attempts performed.
    /// </summary>
    public int RetryCount { get; private set; }

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
    /// <param name="orderId">Order ID associated with this email.</param>
    /// <param name="to">Recipient address.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="body">Email body.</param>
    /// <param name="createdAt">Creation timestamp.</param>
    /// <returns>A <see cref="Result{EmailHistory}"/> indicating success or validation failure.</returns>
    public static Result<EmailHistory> Create(Guid orderId, string to, string subject, string body, DateTime createdAt)
    {
        if (orderId == Guid.Empty)
            return Result<EmailHistory>.Failure(Error.Validation("Order ID is required."));

        if (string.IsNullOrWhiteSpace(to))
            return Result<EmailHistory>.Failure(Error.Validation("Recipient address ('To') is required."));

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
    /// <param name="sentAt">Timestamp of sending.</param>
    public void MarkSent(DateTime sentAt)
    {
        SentAt = sentAt;
        Status = EmailStatus.Sent;
    }

    /// <summary>
    /// Marks the email as failed and increments the retry counter.
    /// </summary>
    public void MarkFailed()
    {
        RetryCount++;
        Status = EmailStatus.Failed;
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

    /// <summary>Failed to send.</summary>
    Failed
}
