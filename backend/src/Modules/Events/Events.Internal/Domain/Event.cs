using Ticketing.SharedKernel;

namespace Events.Internal.Domain;

internal sealed class Event : BaseEntity<Guid>
{
    /// <summary>
    /// Reference tới Identity module — KHÔNG có FK cross-schema.
    /// </summary>
    public Guid OrganizerId { get; private set; }

    public Guid CategoryId { get; private set; }
    public Guid VenueId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>
    /// S3/R2 object key cho ảnh bìa sự kiện.
    /// </summary>
    public string? CoverImageKey { get; private set; }

    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string Status { get; private set; } = EventStatus.Draft;
    public string? RejectReason { get; private set; }

    /// <summary>
    /// Giá thấp nhất — denormalized từ Ticketing module.
    /// </summary>
    public decimal? MinPrice { get; private set; }

    /// <summary>
    /// Giá cao nhất — denormalized từ Ticketing module.
    /// </summary>
    public decimal? MaxPrice { get; private set; }

    // Navigation properties (FK internal)
    public Category Category { get; private set; } = null!;
    public Venue Venue { get; private set; } = null!;

    private Event() { }

    public static Event Create(
        Guid organizerId,
        Guid categoryId,
        Guid venueId,
        string title,
        string slug,
        string? description,
        DateTime startsAt,
        DateTime endsAt) => new()
    {
        Id = Guid.CreateVersion7(),
        OrganizerId = organizerId,
        CategoryId = categoryId,
        VenueId = venueId,
        Title = title,
        Slug = slug,
        Description = description,
        StartsAt = startsAt,
        EndsAt = endsAt,
        Status = EventStatus.Draft,
        CreatedAt = DateTime.UtcNow
    };

    public void SubmitForApproval()
    {
        Status = EventStatus.PendingApproval;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve()
    {
        Status = EventStatus.OnSale;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(string reason)
    {
        Status = EventStatus.Rejected;
        RejectReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void End()
    {
        Status = EventStatus.Ended;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cập nhật khoảng giá vé — được gọi khi nhận TicketTypesChanged từ Ticketing module.
    /// </summary>
    public void UpdatePriceRange(decimal? min, decimal? max)
    {
        MinPrice = min;
        MaxPrice = max;
        UpdatedAt = DateTime.UtcNow;
    }
}
