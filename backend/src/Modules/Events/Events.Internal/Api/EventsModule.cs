using System.Security.Claims;
using Amazon.S3;
using Events.Internal.Features.Events.ChangeEventStatus;
using Events.Internal.Features.Events.CreateEvent;
using Events.Internal.Features.Events.GetEvent;
using Events.Internal.Features.Events.ListEvents;
using Events.Internal.Features.Events.ManageCategory;
using Events.Internal.Features.Events.ManageVenue;
using Events.Internal.Features.Events.UpdateEvent;
using Events.Internal.Features.PriceFilter;
using Events.Internal.Features.Reports;
using Events.Internal.Features.Storage;
using Events.Internal.Infrastructure;
using Events.Internal.Infrastructure.Caching;
using Events.Internal.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ticketing.Contracts;
using Ticketing.Infrastructure.Core;
using Ticketing.Infrastructure.Core.Storage;

namespace Events.Internal.Api;

/// <summary>
/// Điểm vào duy nhất (public) của module Events.
/// Đăng ký DI và map endpoints.
/// </summary>
public static class EventsModule
{
    public static IServiceCollection AddEventsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModuleDbContext<EventsDbContext>(
            connectionStringName: "DefaultConnection",
            schema: EventsDbContext.SchemaName,
            configuration: configuration);

        services.AddModuleValidators(typeof(EventsModule).Assembly);

        // ── R2 / S3 Storage ──────────────────────────────────────────
        services.Configure<R2Options>(configuration.GetSection(R2Options.SectionName));

        var r2Options = configuration.GetSection(R2Options.SectionName).Get<R2Options>();
        if (r2Options is not null && !string.IsNullOrEmpty(r2Options.AccountId))
        {
            services.TryAddSingleton<IAmazonS3>(_ =>
            {
                var config = new AmazonS3Config
                {
                    ServiceURL = r2Options.ServiceUrl,
                    ForcePathStyle = true, // R2 yêu cầu path-style
                };
                return new AmazonS3Client(r2Options.AccessKey, r2Options.SecretKey, config);
            });
        }

        services.AddScoped<IR2StorageService, R2StorageService>();

        // ── Cache Service (Redis / IDistributedCache theo ADR-0004) ────
        services.AddScoped<IEventsCacheService, EventsCacheService>();

        // ── Feature Handlers ─────────────────────────────────────────
        services.AddScoped<CreateEventHandler>();
        services.AddScoped<GetEventHandler>();
        services.AddScoped<ListEventsHandler>();
        services.AddScoped<ListOrganizerEventsHandler>();
        services.AddScoped<UpdateEventHandler>();
        services.AddScoped<ChangeEventStatusHandler>();
        services.AddScoped<GetUploadPresignedUrlHandler>();
        services.AddScoped<ConfirmCoverImageUploadHandler>();
        services.AddScoped<GetCoverImageUrlHandler>();
        services.AddScoped<VenueHandler>();
        services.AddScoped<CategoryHandler>();
        services.AddScoped<GetOrganizerReportHandler>();
        services.AddScoped<GetAdminReportHandler>();
        services.AddScoped<GetEventAnalyticsHandler>();

        // ── Integration Event Handlers ──────────────────────────────
        services.RegisterIntegrationEventHandler<TicketTypesChangedIntegrationEvent, TicketTypesChangedHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var events = app.MapGroup("/api/events").WithTags("Events");
        var categories = app.MapGroup("/api/categories").WithTags("Categories");
        var venues = app.MapGroup("/api/venues").WithTags("Venues");
        var reports = app.MapGroup("/api/reports/events").WithTags("Reports");

        // ── Events: Public ─────────────────────────────────────────────

        // GET /api/events?page=1&pageSize=20&categoryId=...&category=...&city=...&minPrice=...&maxPrice=...&search=...
        events.MapGet("/", async (
            ListEventsHandler handler,
            string? status,
            Guid? categoryId,
            string? category,
            string? city,
            decimal? minPrice,
            decimal? maxPrice,
            string? search,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var query = new ListEventsQuery(status, categoryId, city, minPrice, maxPrice, search, page, pageSize, category);
            var result = await handler.HandleAsync(query, ct);
            return result.ToHttpResult();
        });

        // GET /api/events/{id}
        events.MapGet("/{id:guid}", async (Guid id, GetEventHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.ToHttpResult();
        });

        // GET /api/events/slug/{slug}
        events.MapGet("/slug/{slug}", async (string slug, GetEventHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleBySlugAsync(slug, ct);
            return result.ToHttpResult();
        });

        // GET /api/events/{id}/cover-image-url
        events.MapGet("/{id:guid}/cover-image-url", async (
            Guid id,
            GetCoverImageUrlHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.ToHttpResult();
        });

        // ── Events: Organizer (JWT claim / X-Organizer-Id) ───────────

        // GET /api/events/mine: Organizer xem tất cả sự kiện của mình (mọi trạng thái)
        events.MapGet("/mine", async (
            ListOrganizerEventsHandler handler,
            HttpContext ctx,
            string? status,
            string? search,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (!TryGetOrganizerId(ctx, out var organizerId))
                return Results.Problem("Không xác định được danh tính Organizer (yêu cầu JWT hoặc header X-Organizer-Id).", statusCode: 401);

            var query = new ListOrganizerEventsQuery(organizerId, status, search, page, pageSize);
            var result = await handler.HandleAsync(query, ct);
            return result.ToHttpResult();
        });

        // POST /api/events
        events.MapPost("/", async (
            CreateEventRequest request,
            CreateEventHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!TryGetOrganizerId(ctx, out var organizerId))
                return Results.Problem("Không xác định được danh tính Organizer (yêu cầu JWT hoặc header X-Organizer-Id).", statusCode: 401);

            var result = await handler.HandleAsync(organizerId, request, ct);
            return result.ToHttpResult(201);
        });

        // PUT /api/events/{id}
        events.MapPut("/{id:guid}", async (
            Guid id,
            UpdateEventRequest request,
            UpdateEventHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!TryGetOrganizerId(ctx, out var organizerId))
                return Results.Problem("Không xác định được danh tính Organizer (yêu cầu JWT hoặc header X-Organizer-Id).", statusCode: 401);

            var result = await handler.HandleAsync(id, organizerId, request, ct);
            return result.ToHttpResult(204);
        });

        // POST /api/events/{id}/submit
        events.MapPost("/{id:guid}/submit", async (
            Guid id,
            ChangeEventStatusHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!TryGetOrganizerId(ctx, out var organizerId))
                return Results.Problem("Không xác định được danh tính Organizer (yêu cầu JWT hoặc header X-Organizer-Id).", statusCode: 401);

            var result = await handler.SubmitForApprovalAsync(id, organizerId, ct);
            return result.ToHttpResult(204);
        });

        // POST /api/events/{id}/cancel: Hủy sự kiện (Organizer hoặc Admin)
        events.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelEventRequest? body,
            ChangeEventStatusHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var isAdmin = IsAdmin(ctx);
            Guid? organizerId = null;
            if (!isAdmin)
            {
                if (!TryGetOrganizerId(ctx, out var orgId))
                    return Results.Problem("Yêu cầu quyền Organizer hoặc Admin để hủy sự kiện.", statusCode: 401);
                organizerId = orgId;
            }

            var result = await handler.CancelAsync(id, organizerId, isAdmin, body?.Reason, ct);
            return result.ToHttpResult(204);
        });

        // POST /api/events/{id}/cover-image/presign
        events.MapPost("/{id:guid}/cover-image/presign", async (
            Guid id,
            PresignRequest body,
            GetUploadPresignedUrlHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!TryGetOrganizerId(ctx, out var organizerId))
                return Results.Problem("Không xác định được danh tính Organizer (yêu cầu JWT hoặc header X-Organizer-Id).", statusCode: 401);

            var result = await handler.HandleAsync(id, organizerId, body.ContentType, ct);
            return result.ToHttpResult();
        });

        // POST /api/events/{id}/cover-image/confirm
        events.MapPost("/{id:guid}/cover-image/confirm", async (
            Guid id,
            ConfirmUploadRequest body,
            ConfirmCoverImageUploadHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!TryGetOrganizerId(ctx, out var organizerId))
                return Results.Problem("Không xác định được danh tính Organizer (yêu cầu JWT hoặc header X-Organizer-Id).", statusCode: 401);

            var result = await handler.HandleAsync(id, organizerId, body.ObjectKey, ct);
            return result.ToHttpResult(204);
        });

        // ── Events: Admin ─────────────────────────────────────────────

        // POST /api/events/{id}/approve
        events.MapPost("/{id:guid}/approve", async (
            Guid id,
            ChangeEventStatusHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.ApproveAsync(id, ct);
            return result.ToHttpResult(204);
        });

        // POST /api/events/{id}/reject
        events.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectRequest body,
            ChangeEventStatusHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.RejectAsync(id, body.Reason, ct);
            return result.ToHttpResult(204);
        });

        // POST /api/events/{id}/end
        events.MapPost("/{id:guid}/end", async (
            Guid id,
            ChangeEventStatusHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.EndAsync(id, ct);
            return result.ToHttpResult(204);
        });

        // ── Categories ────────────────────────────────────────────────

        categories.MapGet("/", async (CategoryHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ListAsync(ct);
            return result.ToHttpResult();
        });

        categories.MapPost("/", async (
            CreateCategoryRequest request,
            CategoryHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.CreateAsync(request, ct);
            return result.ToHttpResult(201);
        });

        // ── Venues ────────────────────────────────────────────────────

        venues.MapGet("/", async (string? city, VenueHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ListAsync(city, ct);
            return result.ToHttpResult();
        });

        venues.MapGet("/{id:guid}", async (Guid id, VenueHandler handler, CancellationToken ct) =>
        {
            var result = await handler.GetAsync(id, ct);
            return result.ToHttpResult();
        });

        venues.MapPost("/", async (CreateVenueRequest request, VenueHandler handler, CancellationToken ct) =>
        {
            var result = await handler.CreateAsync(request, ct);
            return result.ToHttpResult(201);
        });

        // ── Reports & Analytics ───────────────────────────────────────

        // GET /api/reports/events/{id:guid}/analytics: Chi tiết doanh thu, vé bán theo hạng, lượt check-in (FR-23)
        reports.MapGet("/{id:guid}/analytics", async (
            Guid id,
            GetEventAnalyticsHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var isAdmin = IsAdmin(ctx);
            Guid? organizerId = null;
            if (!isAdmin && TryGetOrganizerId(ctx, out var orgId))
            {
                organizerId = orgId;
            }

            var result = await handler.HandleAsync(id, organizerId, isAdmin, ct);
            return result.ToHttpResult();
        });

        // GET /api/reports/events/organizer/{organizerId}
        reports.MapGet("/organizer/{organizerId:guid}", async (
            Guid organizerId,
            GetOrganizerReportHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(organizerId, ct);
            return result.ToHttpResult();
        });

        // GET /api/reports/events/admin?from=...&to=...
        reports.MapGet("/admin", async (
            GetAdminReportHandler handler,
            DateTime? from,
            DateTime? to,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(from, to, ct);
            return result.ToHttpResult();
        });

        return app;
    }

    private static bool TryGetOrganizerId(HttpContext ctx, out Guid organizerId)
    {
        organizerId = Guid.Empty;

        // 1. Lấy từ JWT claims (NameIdentifier, sub, id)
        var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? ctx.User.FindFirst("sub")?.Value
                    ?? ctx.User.FindFirst("id")?.Value;

        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out organizerId))
            return true;

        // 2. Fallback đọc header X-Organizer-Id cho development / testing
        if (Guid.TryParse(ctx.Request.Headers["X-Organizer-Id"], out organizerId))
            return true;

        return false;
    }

    private static bool IsAdmin(HttpContext ctx)
    {
        return ctx.User.IsInRole("Admin") ||
               string.Equals(ctx.Request.Headers["X-Role"], "Admin", StringComparison.OrdinalIgnoreCase);
    }
}

// ── Inline request DTOs cho endpoints ─────────────────────────
internal sealed record PresignRequest(string ContentType);
internal sealed record ConfirmUploadRequest(string ObjectKey);
internal sealed record RejectRequest(string Reason);
internal sealed record CancelEventRequest(string? Reason = null);
