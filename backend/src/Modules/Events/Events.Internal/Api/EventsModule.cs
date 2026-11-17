using Ticketing.Contracts;
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
using Events.Internal.Infrastructure.Storage;
using Amazon.S3;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Core;

namespace Events.Internal.Api;

/// <summary>
/// Äiá»ƒm vÃ o duy nháº¥t (public) cá»§a module Events.
/// ÄÄƒng kÃ½ DI vÃ  map endpoints.
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

        // ── R2 / S3 Storage ──────────────────────────────────────────────
        services.Configure<R2Options>(configuration.GetSection(R2Options.SectionName));

        var r2Options = configuration.GetSection(R2Options.SectionName).Get<R2Options>();
        if (r2Options is not null && !string.IsNullOrEmpty(r2Options.AccountId))
        {
            services.AddSingleton<IAmazonS3>(_ =>
            {
                var config = new AmazonS3Config
                {
                    ServiceURL = r2Options.ServiceUrl,
                    ForcePathStyle = true,  // R2 yêu cầu path-style
                };
                return new AmazonS3Client(r2Options.AccessKey, r2Options.SecretKey, config);
            });
            services.AddScoped<IR2StorageService, R2StorageService>();
        }

        // â”€â”€ Feature Handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        services.AddScoped<CreateEventHandler>();
        services.AddScoped<GetEventHandler>();
        services.AddScoped<ListEventsHandler>();
        services.AddScoped<UpdateEventHandler>();
        services.AddScoped<ChangeEventStatusHandler>();
        services.AddScoped<GetUploadPresignedUrlHandler>();
        services.AddScoped<ConfirmCoverImageUploadHandler>();
        services.AddScoped<GetCoverImageUrlHandler>();
        services.AddScoped<VenueHandler>();
        services.AddScoped<CategoryHandler>();
        services.AddScoped<GetOrganizerReportHandler>();
        services.AddScoped<GetAdminReportHandler>();

        // â”€â”€ Integration Event Handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        services.RegisterIntegrationEventHandler<TicketTypesChangedIntegrationEvent, TicketTypesChangedHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var events = app.MapGroup("/api/events").WithTags("Events");
        var categories = app.MapGroup("/api/categories").WithTags("Categories");
        var venues = app.MapGroup("/api/venues").WithTags("Venues");
        var reports = app.MapGroup("/api/reports/events").WithTags("Reports");

        // â”€â”€ Events: Public â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        // GET /api/events?page=1&pageSize=20&categoryId=...&city=...&minPrice=...&maxPrice=...&search=...
        events.MapGet("/", async (
            ListEventsHandler handler,
            string? status,
            Guid? categoryId,
            string? city,
            decimal? minPrice,
            decimal? maxPrice,
            string? search,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var query = new ListEventsQuery(status, categoryId, city, minPrice, maxPrice, search, page, pageSize);
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

        // â”€â”€ Events: Organizer (TODO: thÃªm [Authorize(Roles="Organizer")] á»Ÿ Phase 2) â”€â”€

        // POST /api/events
        events.MapPost("/", async (
            CreateEventRequest request,
            CreateEventHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            // Phase 2: láº¥y organizerId tá»« JWT claim
            // Táº¡m thá»i dÃ¹ng header X-Organizer-Id Ä‘á»ƒ test (sáº½ bá» á»Ÿ Phase 2)
            if (!Guid.TryParse(ctx.Request.Headers["X-Organizer-Id"], out var organizerId))
                return Results.Problem("Thiáº¿u X-Organizer-Id header.", statusCode: 400);

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
            if (!Guid.TryParse(ctx.Request.Headers["X-Organizer-Id"], out var organizerId))
                return Results.Problem("Thiáº¿u X-Organizer-Id header.", statusCode: 400);

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
            if (!Guid.TryParse(ctx.Request.Headers["X-Organizer-Id"], out var organizerId))
                return Results.Problem("Thiáº¿u X-Organizer-Id header.", statusCode: 400);

            var result = await handler.SubmitForApprovalAsync(id, organizerId, ct);
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
            if (!Guid.TryParse(ctx.Request.Headers["X-Organizer-Id"], out var organizerId))
                return Results.Problem("Thiáº¿u X-Organizer-Id header.", statusCode: 400);

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
            if (!Guid.TryParse(ctx.Request.Headers["X-Organizer-Id"], out var organizerId))
                return Results.Problem("Thiáº¿u X-Organizer-Id header.", statusCode: 400);

            var result = await handler.HandleAsync(id, organizerId, body.ObjectKey, ct);
            return result.ToHttpResult(204);
        });

        // â”€â”€ Events: Admin (TODO: thÃªm [Authorize(Roles="Admin")] á»Ÿ Phase 2) â”€â”€

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

        // â”€â”€ Categories â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€ Venues â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€ Reports â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
}

// â”€â”€ Inline request DTOs cho endpoints â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
internal sealed record PresignRequest(string ContentType);
internal sealed record ConfirmUploadRequest(string ObjectKey);
internal sealed record RejectRequest(string Reason);
