using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Ticketing.Modules.Orders.Api;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Infrastructure;
using Ticketing.Tests.Orders.TestSupport;

namespace Ticketing.Tests.Orders.Fixtures;

/// <summary>
/// Real PostgreSQL 16 via Testcontainers. Starts lazily; when Docker is unavailable the
/// container start fails and DockerRequiredFact tests skip instead of failing.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ticketing")
        .WithUsername("ticketing")
        .WithPassword("ChangeMe123!")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            SchemasToInclude = new[] { "orders" }
        });
        await respawner.ResetAsync(connection);
    }
}

public sealed class PostgresOrdersFactory(string connectionString) : WebApplicationFactory<Program>
{
    public FakeTicketingModuleApi Ticketing { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("IntegrationDelivery:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITicketingModuleApi>();
            services.AddSingleton<ITicketingModuleApi>(Ticketing);
        });
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
