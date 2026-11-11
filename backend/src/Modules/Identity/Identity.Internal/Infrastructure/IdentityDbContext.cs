using Identity.Internal.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Internal.Infrastructure;

internal sealed class IdentityDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public const string SchemaName = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        SeedRoles(modelBuilder);
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid>
            {
                Id = new Guid("10000000-0000-0000-0000-000000000001"),
                Name = "Customer",
                NormalizedName = "CUSTOMER",
                ConcurrencyStamp = "f3ce1ab0-16ef-4fd2-85f1-e8ffeb19b30f"
            },
            new IdentityRole<Guid>
            {
                Id = new Guid("10000000-0000-0000-0000-000000000002"),
                Name = "Organizer",
                NormalizedName = "ORGANIZER",
                ConcurrencyStamp = "a750fc65-5193-4c26-907d-0387389300b1"
            },
            new IdentityRole<Guid>
            {
                Id = new Guid("10000000-0000-0000-0000-000000000003"),
                Name = "Staff",
                NormalizedName = "STAFF",
                ConcurrencyStamp = "6d23b629-9568-4ded-adf1-48141a8e7814"
            },
            new IdentityRole<Guid>
            {
                Id = new Guid("10000000-0000-0000-0000-000000000004"),
                Name = "Admin",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "83d6dc57-d2a3-4599-abde-6e1b38f62dc6"
            }
        );
    }
}
