using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ticketing.Modules.Orders.Api.Endpoints;
using Ticketing.Modules.Orders.Api.OutboxDelivery;
using Ticketing.Modules.Orders.Infrastructure;

namespace Ticketing.Modules.Orders.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOrdersModule(builder.Configuration);

        // Dev/test switch: run the module against an in-process database so the
        // payment flow is verifiable without a PostgreSQL server.
        if (string.Equals(builder.Configuration["Persistence:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = builder.Configuration["Persistence:DatabaseName"] ?? "Ticketing.Orders";
            builder.Services.AddDbContext<OrdersDbContext>(options => options.UseInMemoryDatabase(databaseName));
        }
        else
        {
            builder.Services.AddDbContext<OrdersDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
        }

        // Local outbox delivery (TV1's central worker takes over in production).
        if (builder.Configuration.GetValue("IntegrationDelivery:Enabled", true))
        {
            builder.Services.AddHostedService<OutboxDeliveryService>();
        }

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Module = "Orders" }));
        app.MapOrderEndpoints();
        app.MapPaymentEndpoints();

        await app.RunAsync();
    }
}

