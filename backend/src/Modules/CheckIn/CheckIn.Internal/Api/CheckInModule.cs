using System.Security.Claims;
using CheckIn.Contracts;
using CheckIn.Internal.Application;
using CheckIn.Internal.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Core;

namespace CheckIn.Internal.Api;

public sealed record CheckInApiRequest(
    Guid EventId,
    string QrPayload,
    string? Gate = null);

public sealed record CheckInScanRequest(
    string? Code = null,
    Guid? EventId = null,
    string? QrPayload = null,
    string? Gate = null);

public sealed record AssignStaffApiRequest(
    string StaffId);

/// <summary>
/// Điểm vào duy nhất (public) của module CheckIn.
/// Đăng ký DI và map endpoints.
/// </summary>
public static class CheckInModule
{
    public static IServiceCollection AddCheckInModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModuleDbContext<CheckInDbContext>(
            connectionStringName: "DefaultConnection",
            schema: CheckInDbContext.SchemaName,
            configuration: configuration);

        services.AddModuleValidators(typeof(CheckInModule).Assembly);

        services.AddScoped<ICheckInModuleApi, CheckInService>();
        services.AddScoped<CheckInService>();

        return services;
    }

    public static IEndpointRouteBuilder MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        // 1. Quét mã QR soát vé qua Frontend Scan: POST /api/check-in/scan
        group.MapPost("/check-in/scan", async (
            HttpContext context,
            CheckInScanRequest body,
            ICheckInModuleApi checkInApi,
            CheckInDbContext checkInDb,
            CancellationToken ct) =>
        {
            var rawCode = body.Code ?? body.QrPayload ?? string.Empty;
            var eventId = body.EventId ?? Guid.Empty;

            // Nếu chưa có eventId, thử giải mã từ định dạng QR v1.{ticketId}.{eventId}.{hmac}
            if (eventId == Guid.Empty && rawCode.Contains('.'))
            {
                var parts = rawCode.Split('.');
                if (parts.Length >= 4 && Guid.TryParse(parts[2], out var parsedEventId))
                {
                    eventId = parsedEventId;
                }
            }

            var staffId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          context.User.FindFirstValue("sub") ??
                          "staff-terminal";

            var result = await checkInApi.CheckInAsync(
                new CheckInRequestDto(eventId, rawCode, body.Gate),
                staffId,
                ct);

            var totalCount = await checkInDb.CheckInLogs.CountAsync(l => l.Result == "Valid", ct);

            var status = result.Status.Equals("AlreadyUsed", StringComparison.OrdinalIgnoreCase)
                ? "used"
                : result.Status.ToLowerInvariant();

            return Results.Ok(new
            {
                status,
                count = totalCount,
                message = result.Message,
                checkedInAt = result.ScannedAtUtc
            });
        })
        .WithName("CheckInTicketScan")
        .WithTags("CheckIn")
        .AllowAnonymous();

        // 2. Thống kê tổng lượt check-in toàn hệ thống cho Mobile Web App: GET /api/check-in/count
        group.MapGet("/check-in/count", async (
            CheckInDbContext checkInDb,
            CancellationToken ct) =>
        {
            var count = await checkInDb.CheckInLogs.CountAsync(l => l.Result == "Valid", ct);
            return Results.Ok(new { count });
        })
        .WithName("GetGlobalCheckedInCount")
        .WithTags("CheckIn")
        .AllowAnonymous();

        // 3. FR-20: Quét mã QR soát vé tại cổng
        group.MapPost("/checkin", async (
            HttpContext context,
            CheckInApiRequest body,
            ICheckInModuleApi checkInApi,
            CancellationToken ct) =>
        {
            var staffId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          context.User.FindFirstValue("sub") ??
                          "staff-terminal";

            var result = await checkInApi.CheckInAsync(
                new CheckInRequestDto(body.EventId, body.QrPayload, body.Gate),
                staffId,
                ct);

            return Results.Ok(result);
        })
        .WithName("CheckInTicket")
        .WithTags("CheckIn")
        .AllowAnonymous()
        .Produces<CheckInResultDto>(StatusCodes.Status200OK);

        // 4. FR-21: Thống kê số lượng người tham dự đã vào cổng thời gian thực
        group.MapGet("/checkin/{eventId:guid}/count", async (
            Guid eventId,
            ICheckInModuleApi checkInApi,
            CancellationToken ct) =>
        {
            var count = await checkInApi.GetCheckedInCountAsync(eventId, ct);
            return Results.Ok(new { eventId, checkedInCount = count });
        })
        .WithName("GetCheckedInCount")
        .WithTags("CheckIn")
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK);

        // 5. Phân công nhân viên soát vé
        group.MapPost("/events/{eventId:guid}/staff", async (
            Guid eventId,
            AssignStaffApiRequest request,
            ICheckInModuleApi checkInApi,
            CancellationToken ct) =>
        {
            await checkInApi.AssignStaffAsync(eventId, request.StaffId, ct);
            return Results.NoContent();
        })
        .WithName("AssignEventStaff")
        .WithTags("CheckIn")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
