using Klinova.Domain.Billing;
using Klinova.Domain.Common;
using Klinova.Domain.Organization;
using Klinova.Domain.Patients;
using Klinova.Domain.Services;

namespace Klinova.Domain.Visits;

/// <summary>
/// An outpatient visit ("başvuru / sevk"): a patient admitted to a polyclinic on a
/// given day with a queue number, examined by a physician and finally billed.
/// </summary>
/// <remarks>
/// Lifecycle: <see cref="VisitStatus.Waiting"/> → <see cref="VisitStatus.InExamination"/>
/// → <see cref="VisitStatus.Completed"/> → paid. A visit can be cancelled until it
/// is completed. Once a payment is registered the visit is financially closed and
/// its services can no longer change.
/// </remarks>
public class Visit : AuditableEntity
{
    public const int MaxQuantity = 999;

    private readonly List<VisitItem> _items = [];

    private Visit()
    {
    }

    public Visit(int patientId, int polyclinicId, int? doctorId, DateOnly visitDate, int queueNumber, DateTime checkInAtUtc, string? complaint)
    {
        if (patientId <= 0)
        {
            throw new DomainException("Hasta seçilmelidir.");
        }

        if (polyclinicId <= 0)
        {
            throw new DomainException("Poliklinik seçilmelidir.");
        }

        if (queueNumber <= 0)
        {
            throw new DomainException("Sıra numarası pozitif olmalıdır.");
        }

        PatientId = patientId;
        PolyclinicId = polyclinicId;
        DoctorId = doctorId;
        VisitDate = visitDate;
        QueueNumber = queueNumber;
        CheckInAtUtc = checkInAtUtc;
        Complaint = Guard.Optional(complaint, "Şikâyet", FieldLengths.LongText);
        Status = VisitStatus.Waiting;
    }

    public int PatientId { get; private set; }

    public Patient? Patient { get; private set; }

    public int PolyclinicId { get; private set; }

    public Polyclinic? Polyclinic { get; private set; }

    public int? DoctorId { get; private set; }

    public Doctor? Doctor { get; private set; }

    /// <summary>Local business day the visit belongs to; queue numbers restart every day.</summary>
    public DateOnly VisitDate { get; private set; }

    public int QueueNumber { get; private set; }

    public VisitStatus Status { get; private set; }

    public DateTime CheckInAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public string? Complaint { get; private set; }

    public string? Diagnosis { get; private set; }

    public string? ExaminationNotes { get; private set; }

    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<VisitItem> Items => _items.AsReadOnly();

    public Payment? Payment { get; private set; }

    public decimal TotalAmount => _items.Sum(i => i.LineTotal);

    public bool IsPaid => Payment is not null;

    public bool IsOpen => Status is VisitStatus.Waiting or VisitStatus.InExamination;

    /// <summary>Calls the patient into the examination room.</summary>
    public void StartExamination(int? doctorId, DateTime nowUtc)
    {
        if (Status != VisitStatus.Waiting)
        {
            throw new DomainException("Yalnızca sırada bekleyen hastalar muayeneye alınabilir.");
        }

        Status = VisitStatus.InExamination;
        StartedAtUtc = nowUtc;
        if (doctorId is not null)
        {
            DoctorId = doctorId;
        }
    }

    /// <summary>Sends a patient who was called but not examined back to the waiting list.</summary>
    public void ReturnToQueue()
    {
        if (Status != VisitStatus.InExamination)
        {
            throw new DomainException("Yalnızca muayenedeki hastalar sıraya geri alınabilir.");
        }

        Status = VisitStatus.Waiting;
        StartedAtUtc = null;
    }

    public void AssignDoctor(Doctor doctor)
    {
        EnsureNotCancelled();
        if (doctor.PolyclinicId != PolyclinicId)
        {
            throw new DomainException("Hekim, başvurunun yapıldığı poliklinikte görevli değil.");
        }

        DoctorId = doctor.Id;
        Doctor = doctor;
    }

    public void RecordExamination(string? complaint, string? diagnosis, string? notes)
    {
        EnsureNotCancelled();
        Complaint = Guard.Optional(complaint, "Şikâyet", FieldLengths.LongText);
        Diagnosis = Guard.Optional(diagnosis, "Tanı", FieldLengths.LongText);
        ExaminationNotes = Guard.Optional(notes, "Muayene notu", FieldLengths.LongText);
    }

    public VisitItem AddService(MedicalService service, int quantity, DateTime nowUtc)
    {
        EnsureNotCancelled();
        EnsureNotPaid();

        if (!service.IsActive)
        {
            throw new DomainException($"'{service.Name}' hizmeti kullanım dışı.");
        }

        if (quantity is < 1 or > MaxQuantity)
        {
            throw new DomainException($"Miktar 1 ile {MaxQuantity} arasında olmalıdır.");
        }

        var item = new VisitItem(service.Id, service.Code, service.Name, quantity, service.UnitPrice, nowUtc);
        _items.Add(item);
        return item;
    }

    public void RemoveService(int itemId)
    {
        EnsureNotCancelled();
        EnsureNotPaid();

        var item = _items.Find(i => i.Id == itemId)
            ?? throw new DomainException("Hizmet kaydı bulunamadı.");
        _items.Remove(item);
    }

    public void Complete(DateTime nowUtc)
    {
        if (!IsOpen)
        {
            throw new DomainException("Yalnızca açık başvurular tamamlanabilir.");
        }

        StartedAtUtc ??= nowUtc;
        Status = VisitStatus.Completed;
        CompletedAtUtc = nowUtc;
    }

    /// <summary>Reopens a completed but unpaid visit, e.g. to add a forgotten procedure.</summary>
    public void Reopen()
    {
        if (Status != VisitStatus.Completed)
        {
            throw new DomainException("Yalnızca tamamlanmış başvurular yeniden açılabilir.");
        }

        EnsureNotPaid();
        Status = VisitStatus.InExamination;
        CompletedAtUtc = null;
    }

    public void Cancel(string? reason, DateTime nowUtc)
    {
        if (!IsOpen)
        {
            throw new DomainException("Tamamlanmış veya iptal edilmiş başvurular iptal edilemez.");
        }

        CancellationReason = Guard.Required(reason, "İptal nedeni", FieldLengths.ShortText * 2);
        Status = VisitStatus.Cancelled;
        CompletedAtUtc = nowUtc;
    }

    public Payment RegisterPayment(decimal discount, PaymentMethod method, string? note, string? receivedBy, DateTime nowUtc)
    {
        if (Status != VisitStatus.Completed)
        {
            throw new DomainException("Tahsilat yapılabilmesi için muayene tamamlanmış olmalıdır.");
        }

        EnsureNotPaid();

        if (_items.Count == 0)
        {
            throw new DomainException("Hizmet girilmemiş başvuru için tahsilat yapılamaz.");
        }

        var gross = TotalAmount;
        if (discount < 0 || discount > gross)
        {
            throw new DomainException("İndirim tutarı 0 ile toplam tutar arasında olmalıdır.");
        }

        Payment = new Payment(Id, gross, discount, method, Guard.Optional(note, "Açıklama", FieldLengths.ShortText * 2), receivedBy, nowUtc);
        return Payment;
    }

    private void EnsureNotCancelled()
    {
        if (Status == VisitStatus.Cancelled)
        {
            throw new DomainException("İptal edilmiş başvuru üzerinde işlem yapılamaz.");
        }
    }

    private void EnsureNotPaid()
    {
        if (IsPaid)
        {
            throw new DomainException("Tahsilatı yapılmış başvuruda hizmet değişikliği yapılamaz.");
        }
    }
}
