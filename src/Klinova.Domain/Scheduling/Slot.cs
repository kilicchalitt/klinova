namespace Klinova.Domain.Scheduling;

/// <summary>A bookable time slot of a physician on a given day.</summary>
public sealed record Slot(int DoctorId, DateOnly Date, TimeOnly Start, TimeOnly End, bool IsOnlineBookable)
{
    public DateTime StartsAtLocal => Date.ToDateTime(Start);
}
