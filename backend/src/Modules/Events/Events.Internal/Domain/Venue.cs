using Ticketing.SharedKernel;

namespace Events.Internal.Domain;

internal sealed class Venue : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public int? Capacity { get; private set; }

    private Venue() { }

    public static Venue Create(
        string name,
        string address,
        string city,
        double? latitude = null,
        double? longitude = null,
        int? capacity = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Address = address,
        City = city,
        Latitude = latitude,
        Longitude = longitude,
        Capacity = capacity,
        CreatedAt = DateTime.UtcNow
    };
}
