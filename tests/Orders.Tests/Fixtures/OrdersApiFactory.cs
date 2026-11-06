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
        // The host must not inherit developer-machine state: WebApplicationFactory runs
        // in Development by default, which loads the Orders.Api user-secrets store and
        // overrides the VNPay credentials the tests sign with (every signature check
        // then fails with RspCode 97). "Testing" keeps user-secrets out entirely.
        builder.UseEnvironment("Testing");

        // Unique InMemory database per factory: EF InMemory stores are global by name,
        // so tests would otherwise observe each other's data.
        builder.UseSetting("Persistence:Provider", "InMemory");
        builder.UseSetting("Persistence:DatabaseName", $"orders-tests-{Guid.NewGuid():N}");
        builder.UseSetting("IntegrationDelivery:Enabled", "false");

        // Pin the gateway credentials the tests sign with instead of trusting
        // appsettings.json to stay in sync with VnPayTestHelper.
        builder.UseSetting("Payment:VnPay:TmnCode", VnPayTestHelper.TmnCode);
        builder.UseSetting("Payment:VnPay:HashSecret", VnPayTestHelper.HashSecret);
        builder.UseSetting("Payment:MoMo:PartnerCode", MoMoTestHelper.PartnerCode);
        builder.UseSetting("Payment:MoMo:AccessKey", MoMoTestHelper.AccessKey);
        builder.UseSetting("Payment:MoMo:SecretKey", MoMoTestHelper.SecretKey);
        builder.UseSetting("Payment:MoMo:ApiBaseUrl", "https://test-payment.momo.test");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITicketingModuleApi>();
            services.AddSingleton<ITicketingModuleApi>(Ticketing);
        });
    }
}
