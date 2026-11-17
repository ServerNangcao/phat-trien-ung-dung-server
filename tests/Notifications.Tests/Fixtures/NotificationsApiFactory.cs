using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ticketing.Modules.Notifications.Api;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Tests.Notifications.TestSupport;

namespace Ticketing.Tests.Notifications.Fixtures;

/// <summary>
/// Boots Notifications.Api against the EF InMemory provider with a fake SMTP service and
/// the retry worker disabled, so endpoint tests are deterministic without Docker,
/// PostgreSQL, or a mail server.
/// </summary>
public sealed class NotificationsApiFactory : WebApplicationFactory<Program>
{
    public FakeEmailService Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Unique InMemory database per factory: EF InMemory stores are global by name,
        // so tests would otherwise observe each other's data.
        builder.UseSetting("Persistence:Provider", "InMemory");
        builder.UseSetting("Persistence:DatabaseName", $"notifications-tests-{Guid.NewGuid():N}");
        builder.UseSetting("EmailRetry:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Email);
        });
    }
}
