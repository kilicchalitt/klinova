using Klinova.Domain.Common;

namespace Klinova.Domain.Scheduling;

/// <summary>
/// An official or institutional holiday on which no appointments are given.
/// Half days (e.g. the eve of a national holiday) close from <see cref="ClosedFrom"/>.
/// </summary>
public class Holiday : AuditableEntity
{
    public DateOnly Date { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Time from which the day is closed; <c>null</c> for a full-day holiday.</summary>
    public TimeOnly? ClosedFrom { get; set; }

    public bool Closes(DateOnly date, TimeOnly slotStart)
        => Date == date && (ClosedFrom is null || slotStart >= ClosedFrom.Value);
}
