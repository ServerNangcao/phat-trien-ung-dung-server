using Events.Internal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events.Internal.Infrastructure.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events", t =>
        {
            t.HasCheckConstraint("ck_events_dates", "\"EndsAt\" > \"StartsAt\"");
            t.HasCheckConstraint("ck_events_status",
                "\"Status\" IN ('Draft','PendingApproval','OnSale','Ended','Rejected')");
        });

        builder.HasKey(x => x.Id);

        // OrganizerId — cross-schema reference, NO FK
        builder.Property(x => x.OrganizerId)
            .IsRequired();

        // FK internal: Category
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .IsRequired();

        // FK internal: Venue
        builder.HasOne(x => x.Venue)
            .WithMany()
            .HasForeignKey(x => x.VenueId)
            .IsRequired();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Slug)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Description)
            .HasMaxLength(5000);

        builder.Property(x => x.CoverImageKey)
            .HasMaxLength(500);

        builder.Property(x => x.StartsAt)
            .IsRequired();

        builder.Property(x => x.EndsAt)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.MinPrice)
            .HasPrecision(18, 0);

        builder.Property(x => x.MaxPrice)
            .HasPrecision(18, 0);

        // Indexes
        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.HasIndex("Status", "StartsAt");

        builder.HasIndex(x => x.CategoryId);

        builder.HasIndex("MinPrice", "MaxPrice");

        builder.HasIndex("OrganizerId", "Status");

        // NOTE: GIN index trên Title cho full-text search nên được thêm
        // bằng raw SQL trong migration file, không cấu hình ở đây.
        // Ví dụ: migrationBuilder.Sql(
        //     "CREATE INDEX ix_events_title_gin ON events.\"Events\" USING gin (to_tsvector('simple', \"Title\"));"
        // );
    }
}
