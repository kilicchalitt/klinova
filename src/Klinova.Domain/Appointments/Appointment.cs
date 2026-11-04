using System.Security.Cryptography;
using Klinova.Domain.Common;
using Klinova.Domain.Organization;
using Klinova.Domain.Patients;

namespace Klinova.Domain.Appointments;

/// <summary>
/// A booked time slot with a physician. On the appointment day the patient is
/// checked in at the desk, which turns the appointment into a <see cref="Visits.Visit"/>.
/// </summary>
public class Appointment : AuditableEntity
{
    public const int NoteMaxLength = 500;
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private Appointment()
    {
    }

    public Appointment(int patientId, int doctorId, int polyclinicId, DateOnly date, TimeOnly start, TimeOnly end, AppointmentChannel channel, string? patientNote)
    {
        if (patientId <= 0 || doctorId <= 0 || polyclinicId <= 0)
        {
            throw new DomainException("Hasta, hekim ve poliklinik seçilmelidir.");
        }

        if (end <= start)
        {
            throw new DomainException("Randevu bitiş saati başlangıçtan sonra olmalıdır.");
        }

        PatientId = patientId;
        DoctorId = doctorId;
        PolyclinicId = polyclinicId;
        Date = date;
        StartTime = start;
        EndTime = end;
        Channel = channel;
        PatientNote = Guard.Optional(patientNote, "Hekime not", NoteMaxLength);
        Status = AppointmentStatus.Booked;
        ConfirmationCode = NewCode();
    }

    public int PatientId { get; private set; }

    public Patient? Patient { get; private set; }

    public int DoctorId { get; private set; }

    public Doctor? Doctor { get; private set; }

    public int PolyclinicId { get; private set; }

    public Polyclinic? Polyclinic { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public AppointmentChannel Channel { get; private set; }

    /// <summary>Short code given to the patient, e.g. for phone enquiries ("randevu kodu").</summary>
    public string ConfirmationCode { get; private set; } = string.Empty;

    /// <summary>Optional note from the patient to the physician ("hekime not").</summary>
    public string? PatientNote { get; private set; }

    public CancellationSource? CancelledBy { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public DateTime? CheckedInAtUtc { get; private set; }

    /// <summary>The visit created when the patient was checked in.</summary>
    public int? VisitId { get; private set; }

    public DateTime? ReminderSentAtUtc { get; private set; }

    /// <summary>Set when this appointment replaces another one (rescheduling).</summary>
    public int? RescheduledFromId { get; private set; }

    public bool IsActive => Status == AppointmentStatus.Booked;

    public DateTime StartsAtLocal => Date.ToDateTime(StartTime);

    public void Cancel(CancellationSource source, string? reason, DateTime nowUtc)
    {
        if (Status != AppointmentStatus.Booked)
        {
            throw new DomainException("Yalnızca aktif randevular iptal edilebilir.");
        }

        Status = AppointmentStatus.Cancelled;
        CancelledBy = source;
        CancellationReason = Guard.Optional(reason, "İptal nedeni", FieldLengths.ShortText * 2);
        CancelledAtUtc = nowUtc;
    }

    public void CheckIn(int visitId, DateOnly today, DateTime nowUtc)
    {
        if (Status != AppointmentStatus.Booked)
        {
            throw new DomainException("Yalnızca aktif randevular için hasta kabulü yapılabilir.");
        }

        if (Date != today)
        {
            throw new DomainException("Randevu kabulü yalnızca randevu gününde yapılabilir.");
        }

        Status = AppointmentStatus.CheckedIn;
        VisitId = visitId;
        CheckedInAtUtc = nowUtc;
    }

    public void MarkNoShow(DateTime nowLocal)
    {
        if (Status != AppointmentStatus.Booked)
        {
            throw new DomainException("Yalnızca aktif randevular 'gelmedi' olarak işaretlenebilir.");
        }

        if (StartsAtLocal > nowLocal)
        {
            throw new DomainException("Saati gelmemiş bir randevu 'gelmedi' olarak işaretlenemez.");
        }

        Status = AppointmentStatus.NoShow;
    }

    public void MarkReminderSent(DateTime nowUtc) => ReminderSentAtUtc = nowUtc;

    public void LinkToPrevious(int previousAppointmentId) => RescheduledFromId = previousAppointmentId;

    private static string NewCode()
    {
        Span<char> code = stackalloc char[8];
        for (var i = 0; i < code.Length; i++)
        {
            code[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        return new string(code);
    }
}
