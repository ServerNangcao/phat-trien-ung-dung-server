using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ticketing.Modules.Notifications.Api.Background;
using Ticketing.Modules.Notifications.Api.Endpoints;
using Ticketing.Modules.Notifications.Infrastructure;

namespace Ticketing.Modules.Notifications.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddNotificationsModule(builder.Configuration);

        // Dev/test switch: run the module against an in-process database so the
        // email flow is verifiable without a PostgreSQL server.
        if (string.Equals(builder.Configuration["Persistence:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = builder.Configuration["Persistence:DatabaseName"] ?? "Ticketing.Notifications";
            builder.Services.AddDbContext<NotificationsDbContext>(options => options.UseInMemoryDatabase(databaseName));
        }
        else
        {
            builder.Services.AddDbContext<NotificationsDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
        }

        // Background retry loop; gated so tests can drive retries deterministically.
        if (builder.Configuration.GetValue("EmailRetry:Enabled", true))
        {
            builder.Services.AddHostedService<EmailRetryWorker>();
        }

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Module = "Notifications" }));
        app.MapNotificationEndpoints();
        app.MapIntegrationEventEndpoints();

        await app.RunAsync();
    }
}
