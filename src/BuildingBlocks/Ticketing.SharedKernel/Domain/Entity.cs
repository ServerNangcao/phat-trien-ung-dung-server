namespace Ticketing.SharedKernel.Domain;

public abstract class Entity
{
    public Guid Id { get; protected init; }

    protected Entity(Guid id)
    {
        Id = id;
    }

    public override bool Equals(object? obj)
    {
        // Runtime-type check: an Order and a RefundRequest sharing a Guid are not equal.
        return obj is Entity other && GetType() == other.GetType() && Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}