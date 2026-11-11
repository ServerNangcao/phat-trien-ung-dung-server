using Events.Internal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events.Internal.Infrastructure.Configurations;

internal sealed class EventStaffConfiguration : IEntityTypeConfiguration<EventStaff>
{
    public void Configure(EntityTypeBuilder<EventStaff> builder)
    {
        builder.ToTable("EventStaff");

        builder.HasKey(x => new { x.EventId, x.StaffUserId });

        // FK internal: Event
        builder.HasOne(x => x.Event)
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .IsRequired();

        // StaffUserId — cross-schema reference, NO FK
        builder.Property(x => x.StaffUserId)
            .IsRequired();
    }
}
