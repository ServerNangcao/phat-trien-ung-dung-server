using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Ticketing.Modules.Notifications.Application.Data;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Contracts;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications");

        // Send a transactional email (admin/internal services). Same lifecycle as the
        // ticket emails: record → send → mark → commit, with retry scheduling on failure.
        group.MapPost("/send", async (
            [FromBody] SendEmailRequest request,
            [FromServices] IEmailRepository emailRepository,
            [FromServices] IEmailService emailService,
            [FromServices] IUnitOfWork unitOfWork,
            [FromServices] IClock clock) =>
        {
            var record = EmailHistory.Create(request.OrderId, request.To, request.Subject, request.Body, clock.UtcNow);
            if (record.IsFailure)
                return Results.BadRequest(new { error = record.Error.Message });

            var email = record.Value;
            await emailRepository.AddAsync(email);

            try
            {
                await emailService.SendAsync(request.To, request.Subject, request.Body, true);
                email.MarkSent(clock.UtcNow);
            }
            catch (Exception ex)
            {
                email.MarkFailed(ex.Message);
                email.ScheduleRetry(clock.UtcNow.AddSeconds(10));
            }

            await emailRepository.UpdateAsync(email);
            await unitOfWork.CommitAsync();

            return Results.Ok(new { EmailId = email.Id, Status = email.Status.ToString() });
        })
        .WithName("SendEmail");

        // Query email history for an order
        group.MapGet("/order/{orderId:guid}", async (
            [FromRoute] Guid orderId,
            [FromServices] IEmailRepository emailRepository) =>
        {
            var records = await emailRepository.ListByOrderAsync(orderId);
            return Results.Ok(records.Select(MapToDto));
        })
        .WithName("ListEmailsByOrder");
    }

    private static EmailHistoryDto MapToDto(EmailHistory entity) => new(
        entity.Id,
        entity.OrderId,
        entity.To,
        entity.Subject,
        entity.Body,
        entity.Status.ToString(),
        entity.SentAt,
        entity.CreatedAt,
        entity.RetryCount,
        entity.NextRetryAt,
        entity.LastError);
}

public sealed record SendEmailRequest(Guid OrderId, string To, string Subject, string Body);
