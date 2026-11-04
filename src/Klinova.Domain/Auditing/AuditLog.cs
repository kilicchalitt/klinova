namespace Klinova.Domain.Auditing;

/// <summary>Immutable record of a data change, written automatically on every save.</summary>
public class AuditLog
{
    public long Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public AuditAction Action { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    /// <summary>JSON document with the changed properties (old and new values).</summary>
    public string? Changes { get; set; }
}

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
}
