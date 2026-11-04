namespace Klinova.Domain.Common;

/// <summary>Base type for every persisted aggregate or entity with an integer key.</summary>
public abstract class Entity
{
    public int Id { get; protected set; }
}
