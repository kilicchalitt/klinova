namespace Klinova.Domain.Common;

/// <summary>
/// Entity whose creation and modification metadata is maintained automatically
/// by the persistence layer.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }
}
