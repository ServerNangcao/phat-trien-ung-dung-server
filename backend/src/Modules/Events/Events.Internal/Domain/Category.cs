using Ticketing.SharedKernel;

namespace Events.Internal.Domain;

internal sealed class Category : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    private Category() { }

    public static Category Create(string name, string slug) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Slug = slug,
        CreatedAt = DateTime.UtcNow
    };
}
