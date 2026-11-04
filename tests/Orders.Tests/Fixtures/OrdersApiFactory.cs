using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ticketing.Modules.Orders.Api;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Tests.Orders.TestSupport;

namespace Ticketing.Tests.Orders.Fixtures;

/// <summary>
/// Boots Orders.Api against the EF InMemory provider with a fake Ticketing module and
/// the outbox delivery worker disabled, so endpoint tests are deterministic and run
/// without Docker or PostgreSQL.
/// </summary>
public sealed class OrdersApiFactory : WebApplicationFactory<Program>
{
    public FakeTicketingModuleApi Ticketing { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Unique InMemory database per factory: EF InMemory stores are global by name,
        // so tests would otherwise observe each other's data.
        builder.UseSetting("Persistence:Provider", "InMemory");
        builder.UseSetting("Persistence:DatabaseName", $"orders-tests-{Guid.NewGuid():N}");
        builder.UseSetting("IntegrationDelivery:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITicketingModuleApi>();
            services.AddSingleton<ITicketingModuleApi>(Ticketing);
        });
    }
}
