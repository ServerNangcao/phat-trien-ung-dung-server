using CheckIn.Internal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckIn.Internal.Infrastructure.Configurations;

internal sealed class CheckInLogConfiguration : IEntityTypeConfiguration<CheckInLog>
{
    public void Configure(EntityTypeBuilder<CheckInLog> builder)
    {
        builder.ToTable("CheckInLogs", t =>
        {
            t.HasCheckConstraint("ck_checkinlogs_result",
                "\"Result\" IN ('Valid', 'AlreadyUsed', 'Invalid')");
        });

        builder.HasKey(x => x.Id);

        // Cross-schema references, NO FK
        builder.Property(x => x.TicketId);

        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.StaffId)
            .IsRequired();

        builder.Property(x => x.Result)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ScannedAt)
            .IsRequired();

        builder.Property(x => x.DeviceInfo)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(x => new { x.EventId, x.ScannedAt })
            .HasDatabaseName("ix_checkinlogs_event_scanned");

        builder.HasIndex(x => x.TicketId)
            .HasDatabaseName("ix_checkinlogs_ticket");

        // Partial index cho bộ đếm "Đã vào" (FR-21)
        builder.HasIndex(x => x.EventId)
            .HasDatabaseName("ix_checkinlogs_event_valid")
            .HasFilter("\"Result\" = 'Valid'");
    }
}

