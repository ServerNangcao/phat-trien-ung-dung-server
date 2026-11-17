using Identity.Internal.Api;
using Events.Internal.Api;
using Ticketing.Internal.Api;
using Orders.Internal.Api;
using CheckIn.Internal.Api;
using Notifications.Internal.Api;
using Ticketing.Infrastructure.Core;

var builder = WebApplication.CreateBuilder(args);

// ───────────────────────────────────────────────
// 1. Dịch vụ dùng chung từ BuildingBlocks
// ───────────────────────────────────────────────
builder.Services.AddInfrastructureCore(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// ───────────────────────────────────────────────
// 2. Nạp từng module nghiệp vụ
// ───────────────────────────────────────────────
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddEventsModule(builder.Configuration)
    .AddTicketingModule(builder.Configuration)
    .AddOrdersModule(builder.Configuration)
    .AddCheckInModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration);

var app = builder.Build();

// ───────────────────────────────────────────────
// 3. Middleware Pipeline
// ───────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// ───────────────────────────────────────────────
// 4. Health Check
// ───────────────────────────────────────────────
app.MapHealthChecks("/health");

// ───────────────────────────────────────────────
// 5. Map Endpoints của từng module
// ───────────────────────────────────────────────
app.MapIdentityEndpoints();
app.MapEventsEndpoints();
app.MapTicketingEndpoints();
app.MapOrdersEndpoints();
app.MapCheckInEndpoints();
// Notifications không có HTTP endpoint — chỉ lắng nghe Integration Events

app.Run();
