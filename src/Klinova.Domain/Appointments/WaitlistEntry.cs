using Klinova.Domain.Common;
using Klinova.Domain.Organization;
using Klinova.Domain.Patients;

namespace Klinova.Domain.Appointments;

/// <summary>
/// A request to be notified when a slot becomes free in a polyclinic (optionally
/// with a specific physician) within a date range ("randevu bekleme listesi").
/// </summary>
public class WaitlistEntry : AuditableEntity
{
    private WaitlistEntry()
    {
    }

    public WaitlistEntry(int patientId, int polyclinicId, int? doctorId, DateOnly earliestDate, DateOnly latestDate)
    {
        if (latestDate < earliestDate)
        {
            throw new DomainException("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        }

        PatientId = patientId;
        PolyclinicId = polyclinicId;
        DoctorId = doctorId;
        EarliestDate = earliestDate;
        LatestDate = latestDate;
        Status = WaitlistStatus.Active;
    }

    public int PatientId { get; private set; }

    public Patient? Patient { get; private set; }

    public int PolyclinicId { get; private set; }

    public Polyclinic? Polyclinic { get; private set; }

    public int? DoctorId { get; private set; }

    public Doctor? Doctor { get; private set; }

    public DateOnly EarliestDate { get; private set; }

    public DateOnly LatestDate { get; private set; }

    public WaitlistStatus Status { get; private set; }

    public DateTime? NotifiedAtUtc { get; private set; }

    public bool IsOpen => Status is WaitlistStatus.Active or WaitlistStatus.Notified;

    public bool Matches(int polyclinicId, int doctorId, DateOnly date)
        => IsOpen
           && PolyclinicId == polyclinicId
           && (DoctorId is null || DoctorId == doctorId)
           && date >= EarliestDate
           && date <= LatestDate;

    public void MarkNotified(DateTime nowUtc)
    {
        Status = WaitlistStatus.Notified;
        NotifiedAtUtc = nowUtc;
    }

    public void MarkFulfilled() => Status = WaitlistStatus.Fulfilled;

    public void Withdraw()
    {
        if (!IsOpen)
        {
            throw new DomainException("Bu bekleme kaydı zaten kapanmış.");
        }

        Status = WaitlistStatus.Cancelled;
    }

    public void Expire() => Status = WaitlistStatus.Expired;
}
