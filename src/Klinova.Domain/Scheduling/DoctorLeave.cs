using Klinova.Domain.Common;
using Klinova.Domain.Organization;

namespace Klinova.Domain.Scheduling;

/// <summary>A period in which a physician does not accept appointments (leave, congress, duty).</summary>
public class DoctorLeave : AuditableEntity
{
    private DoctorLeave()
    {
    }

    public DoctorLeave(int doctorId, DateOnly startDate, DateOnly endDate, string? reason)
    {
        if (endDate < startDate)
        {
            throw new DomainException("İzin bitiş tarihi başlangıçtan önce olamaz.");
        }

        DoctorId = doctorId;
        StartDate = startDate;
        EndDate = endDate;
        Reason = Guard.Optional(reason, "Açıklama", FieldLengths.ShortText);
    }

    public int DoctorId { get; private set; }

    public Doctor? Doctor { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public string? Reason { get; private set; }

    public bool Covers(DateOnly date) => date >= StartDate && date <= EndDate;
}
