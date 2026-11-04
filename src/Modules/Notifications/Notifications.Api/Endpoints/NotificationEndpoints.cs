using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MediatR;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Contracts;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;

namespace Ticketing.Modules.Notifications.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications");

        // Send a test/transactional email (used by admin or internal services)
        group.MapPost("/send", async (
            [FromBody] SendEmailRequest request,
            [FromServices] IEmailRepository emailRepository,
            [FromServices] IEmailService emailService,
            [FromServices] Ticketing.SharedKernel.IClock clock) =>
        {
            var record = Ticketing.Modules.Notifications.Domain.Entities.EmailHistory.Create(
                request.OrderId,
                request.To,
                request.Subject,
                request.Body,
                clock.UtcNow);

            if (record.IsFailure)
                return Results.BadRequest(new { error = record.Error.Message });

            var dto = new EmailHistoryDto(
                record.Value.Id,
                record.Value.OrderId,
                record.Value.To,
                record.Value.Subject,
                record.Value.Body,
                record.Value.Status.ToString(),
                record.Value.SentAt,
                record.Value.CreatedAt,
                record.Value.RetryCount);

            await emailRepository.AddAsync(dto);

            try
            {
                await emailService.SendAsync(request.To, request.Subject, request.Body, true);
                record.Value.MarkSent(clock.UtcNow);
            }
            catch
            {
                record.Value.MarkFailed();
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }

            await emailRepository.UpdateAsync(dto with { Status = "Sent", SentAt = clock.UtcNow });
            return Results.Ok(new { EmailId = record.Value.Id });
        })
        .WithName("SendEmail");

        // Query email history for an order
        group.MapGet("/order/{orderId:guid}", async (
            [FromRoute] Guid orderId,
            [FromServices] IEmailRepository emailRepository) =>
        {
            var records = await emailRepository.ListByOrderAsync(orderId);
            return Results.Ok(records);
        })
        .WithName("ListEmailsByOrder");
    }
}

public sealed record SendEmailRequest(Guid OrderId, string To, string Subject, string Body);