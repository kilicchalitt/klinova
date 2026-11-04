using Klinova.Domain.Common;
using Klinova.Domain.Organization;

namespace Klinova.Domain.Scheduling;

/// <summary>
/// A recurring weekly working block of a physician ("çalışma cetveli"), e.g.
/// every Monday 09:00–12:00 in 15-minute appointment slots.
/// </summary>
public class WorkSchedule : AuditableEntity
{
    public const int MinSlotMinutes = 5;
    public const int MaxSlotMinutes = 240;

    private WorkSchedule()
    {
    }

    public WorkSchedule(int doctorId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end, int slotMinutes, DateOnly validFrom, DateOnly? validUntil, bool isOnlineBookable)
    {
        if (doctorId <= 0)
        {
            throw new DomainException("Hekim seçilmelidir.");
        }

        DoctorId = doctorId;
        Update(dayOfWeek, start, end, slotMinutes, validFrom, validUntil, isOnlineBookable);
    }

    public int DoctorId { get; private set; }

    public Doctor? Doctor { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public int SlotMinutes { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    public DateOnly? ValidUntil { get; private set; }

    /// <summary>When false the slots can only be booked by staff (phone / desk).</summary>
    public bool IsOnlineBookable { get; private set; }

    public int SlotCount => (int)((EndTime - StartTime).TotalMinutes / SlotMinutes);

    public void Update(DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end, int slotMinutes, DateOnly validFrom, DateOnly? validUntil, bool isOnlineBookable)
    {
        if (end <= start)
        {
            throw new DomainException("Bitiş saati başlangıç saatinden sonra olmalıdır.");
        }

        if (slotMinutes is < MinSlotMinutes or > MaxSlotMinutes)
        {
            throw new DomainException($"Randevu süresi {MinSlotMinutes} ile {MaxSlotMinutes} dakika arasında olmalıdır.");
        }

        if ((end - start).TotalMinutes < slotMinutes)
        {
            throw new DomainException("Çalışma aralığı en az bir randevu süresi kadar olmalıdır.");
        }

        if (validUntil is { } until && until < validFrom)
        {
            throw new DomainException("Geçerlilik bitişi başlangıçtan önce olamaz.");
        }

        DayOfWeek = dayOfWeek;
        StartTime = start;
        EndTime = end;
        SlotMinutes = slotMinutes;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        IsOnlineBookable = isOnlineBookable;
    }

    public bool AppliesTo(DateOnly date)
        => date.DayOfWeek == DayOfWeek && date >= ValidFrom && (ValidUntil is null || date <= ValidUntil);

    public bool Overlaps(WorkSchedule other)
        => other.DoctorId == DoctorId
           && other.DayOfWeek == DayOfWeek
           && other.StartTime < EndTime
           && StartTime < other.EndTime
           && other.ValidFrom <= (ValidUntil ?? DateOnly.MaxValue)
           && ValidFrom <= (other.ValidUntil ?? DateOnly.MaxValue);
}
