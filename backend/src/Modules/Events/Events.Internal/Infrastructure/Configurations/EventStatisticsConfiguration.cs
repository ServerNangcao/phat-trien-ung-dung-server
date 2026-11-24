using Events.Internal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events.Internal.Infrastructure.Configurations;

internal sealed class EventStatisticsConfiguration : IEntityTypeConfiguration<EventStatistics>
{
    public void Configure(EntityTypeBuilder<EventStatistics> builder)
    {
        builder.ToTable("EventStatistics");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventId)
            .IsRequired();

        builder.HasIndex(x => x.EventId)
            .IsUnique();

        builder.Property(x => x.TotalRevenue)
            .HasPrecision(18, 0)
            .HasDefaultValue(0);

        builder.Property(x => x.TotalTicketsSold)
            .HasDefaultValue(0);

        builder.Property(x => x.TotalCheckedIn)
            .HasDefaultValue(0);

        builder.Property(x => x.TiersBreakdownJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.LastCalculatedAt)
            .IsRequired();
    }
}
