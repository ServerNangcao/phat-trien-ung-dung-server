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
            builder.Services.AddDbContext<NotificationsDbContext>(options => options.UseInMemoryDatabase("Ticketing.Notifications"));
        }
        else
        {
            builder.Services.AddDbContext<NotificationsDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
        }

        builder.Services.AddHostedService<EmailRetryWorker>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Module = "Notifications" }));
        app.MapNotificationEndpoints();
        app.MapIntegrationEventEndpoints();

        await app.RunAsync();
    }
}
