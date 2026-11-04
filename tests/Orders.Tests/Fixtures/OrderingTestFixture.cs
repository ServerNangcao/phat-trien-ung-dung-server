using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Ticketing.Tests.Integration;

public class OrderingTestFixture : IDisposable
{
    public PostgreSqlContainer DbContainer { get; }
    public DbContextOptions<TicketingDbContext> Options { get; }

    public OrderingTestFixture()
    {
        DbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithPort(5432)
            .WithPassword("ChangeMe123!")
            .WithUsername("ticketing")
            .Build();

        Options = new DbContextOptionsBuilder<TicketingDbContext>()
            .UseNpgsql(DbContainer.GetConnectionString())
            .Options;
    }

    public TicketingDbContext CreateContext()
    {
        var context = new TicketingDbContext(Options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose()
    {
        DbContainer.DisposeAsync().GetAwaiter().GetResult();
    }
}