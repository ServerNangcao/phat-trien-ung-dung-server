using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CheckIn.Contracts;
using CheckIn.Internal.Application;
using CheckIn.Internal.Infrastructure;
using Ticketing.Infrastructure.Core;

namespace CheckIn.Internal.Api;

/// <summary>
/// Điểm vào duy nhất (public) của module CheckIn.
/// Đăng ký DI và map endpoints (FR-20, FR-21).
/// </summary>
public static class CheckInModule
{
    public static IServiceCollection AddCheckInModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Đăng ký DbContext với schema "checkin" riêng biệt
        services.AddModuleDbContext<CheckInDbContext>(
            connectionStringName: "DefaultConnection",
            schema: CheckInDbContext.SchemaName,
            configuration: configuration);

        // 2. Service nghiệp vụ
        services.AddScoped<ICheckInModuleApi, CheckInService>();
        services.AddScoped<CheckInService>();

        // 3. Quét validators
        services.AddModuleValidators(typeof(CheckInModule).Assembly);

        return services;
    }

    public static IEndpointRouteBuilder MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        // FR-20: Quét mã QR soát vé
        app.MapPost("/api/checkin", async (
            CheckInRequestDto request,
            HttpContext httpContext,
            CheckInService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.QrPayload))
            {
                return Results.BadRequest(new { message = "Dữ liệu quét mã QR không được để trống." });
            }

            var staffId = httpContext.User.FindFirst("sub")?.Value ?? "system-scanner";
            var result = await service.CheckInAsync(request, staffId, ct);

            return Results.Ok(result);
        });

        // FR-21: Bộ đếm lượt check-in
        app.MapGet("/api/checkin/{eventId:guid}/count", async (
            Guid eventId,
            CheckInService service,
            CancellationToken ct) =>
        {
            var count = await service.GetCheckedInCountAsync(eventId, ct);
            return Results.Ok(new { eventId, checkedIn = count });
        });

        // Phân công nhân viên kiểm soát
        app.MapPost("/api/events/{eventId:guid}/staff", async (
            Guid eventId,
            AssignStaffRequest request,
            CheckInService service,
            CancellationToken ct) =>
        {
            await service.AssignStaffAsync(eventId, request.StaffId, ct);
            return Results.NoContent();
        });

        return app;
    }

    public sealed record AssignStaffRequest(string StaffId);
}
