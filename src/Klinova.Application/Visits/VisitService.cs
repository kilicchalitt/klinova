using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Domain.Common;
using Klinova.Domain.Patients;
using Klinova.Domain.Visits;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Visits;

/// <summary>Admission, queue management and examination workflow.</summary>
public sealed class VisitService(IAppDbContext db, IClock clock)
{
    private const int MaxQueueNumberAttempts = 5;

    /// <summary>
    /// Admits a patient to a polyclinic and assigns the next queue number of the day.
    /// Queue numbers are protected by a unique index; concurrent admissions retry.
    /// </summary>
    public async Task<Visit> AdmitAsync(AdmissionInput input, CancellationToken ct = default)
    {
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PatientId, ct)
            ?? throw NotFoundException.For("Hasta", input.PatientId);
        if (patient.IsArchived)
        {
            throw new DomainException("Arşivlenmiş hasta için başvuru açılamaz. Önce hastayı arşivden çıkarın.");
        }

        var polyclinic = await db.Polyclinics.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PolyclinicId, ct)
            ?? throw NotFoundException.For("Poliklinik", input.PolyclinicId);
        if (!polyclinic.IsActive)
        {
            throw new DomainException($"{polyclinic.Name} polikliniği şu anda hasta kabul etmiyor.");
        }

        if (input.DoctorId is { } doctorId
            && !await db.Doctors.AnyAsync(d => d.Id == doctorId && d.IsActive && d.PolyclinicId == input.PolyclinicId, ct))
        {
            throw new DomainException("Seçilen hekim bu poliklinikte görevli değil.");
        }

        var today = clock.Today;
        var alreadyQueued = await db.Visits.AnyAsync(
            v => v.PatientId == input.PatientId && v.PolyclinicId == input.PolyclinicId && v.VisitDate == today
                 && (v.Status == VisitStatus.Waiting || v.Status == VisitStatus.InExamination),
            ct);
        if (alreadyQueued)
        {
            throw new DomainException($"Hastanın bugün {polyclinic.Name} polikliniğinde açık bir başvurusu zaten var.");
        }

        var serviceIds = input.ServiceIds.Distinct().ToList();
        var services = await db.MedicalServices.Where(s => serviceIds.Contains(s.Id)).ToListAsync(ct);

        for (var attempt = 1; ; attempt++)
        {
            var nextNumber = await db.Visits
                .Where(v => v.PolyclinicId == input.PolyclinicId && v.VisitDate == today)
                .MaxAsync(v => (int?)v.QueueNumber, ct) ?? 0;

            var visit = new Visit(input.PatientId, input.PolyclinicId, input.DoctorId, today, nextNumber + 1, clock.UtcNow, input.Complaint);
            foreach (var service in services)
            {
                visit.AddService(service, 1, clock.UtcNow);
            }

            db.Visits.Add(visit);
            try
            {
                await db.SaveChangesAsync(ct);
                return visit;
            }
            catch (DbUpdateException) when (attempt < MaxQueueNumberAttempts)
            {
                // Another desk took the same queue number at the same moment: try the next one.
                db.DiscardChanges();
            }
        }
    }

    public async Task<List<VisitListItem>> ListAsync(VisitQuery query, CancellationToken ct = default)
    {
        var visits = db.Visits.AsNoTracking();

        if (query.Date is { } date)
        {
            visits = visits.Where(v => v.VisitDate == date);
        }

        if (query.PolyclinicId is { } polyclinicId)
        {
            visits = visits.Where(v => v.PolyclinicId == polyclinicId);
        }

        if (query.DoctorId is { } doctorId)
        {
            visits = visits.Where(v => v.DoctorId == doctorId || v.DoctorId == null);
        }

        if (query.Status is { } status)
        {
            visits = visits.Where(v => v.Status == status);
        }

        if (query.PatientId is { } patientId)
        {
            visits = visits.Where(v => v.PatientId == patientId);
        }

        var rows = await visits
            .OrderBy(v => v.VisitDate).ThenBy(v => v.Polyclinic!.Name).ThenBy(v => v.QueueNumber)
            .Select(v => new
            {
                v.Id,
                v.VisitDate,
                v.QueueNumber,
                v.PatientId,
                PatientFirst = v.Patient!.FirstName,
                PatientLast = v.Patient.LastName,
                v.Patient.MedicalAlerts,
                v.PolyclinicId,
                PolyclinicName = v.Polyclinic!.Name,
                v.DoctorId,
                DoctorTitle = v.Doctor != null ? v.Doctor.Title : null,
                DoctorFirst = v.Doctor != null ? v.Doctor.FirstName : null,
                DoctorLast = v.Doctor != null ? v.Doctor.LastName : null,
                v.Status,
                v.CheckInAtUtc,
                Lines = v.Items.Select(i => new { i.Quantity, i.UnitPrice }).ToList(),
                IsPaid = v.Payment != null,
            })
            .ToListAsync(ct);

        return rows.Select(v => new VisitListItem(
                v.Id,
                v.VisitDate,
                v.QueueNumber,
                v.PatientId,
                Patient.FormatFileNumber(v.PatientId),
                $"{v.PatientFirst} {v.PatientLast}",
                v.MedicalAlerts,
                v.PolyclinicId,
                v.PolyclinicName,
                v.DoctorId,
                v.DoctorFirst is null ? null : DoctorService.FullName(v.DoctorTitle, v.DoctorFirst, v.DoctorLast!),
                v.Status,
                v.CheckInAtUtc,
                v.Lines.Sum(l => l.Quantity * l.UnitPrice),
                v.IsPaid))
            .ToList();
    }

    /// <summary>Loads a visit with patient, polyclinic, physician, services and payment.</summary>
    public async Task<Visit> GetDetailsAsync(int id, CancellationToken ct = default)
        => await db.Visits.AsNoTracking()
               .Include(v => v.Patient)
               .Include(v => v.Polyclinic)
               .Include(v => v.Doctor)
               .Include(v => v.Items)
               .Include(v => v.Payment)
               .AsSplitQuery()
               .FirstOrDefaultAsync(v => v.Id == id, ct)
           ?? throw NotFoundException.For("Başvuru", id);

    /// <summary>Calls the next waiting patient of a polyclinic into the examination room.</summary>
    public async Task<int?> CallNextAsync(int polyclinicId, int? doctorId, CancellationToken ct = default)
    {
        var today = clock.Today;
        var next = await db.Visits
            .Where(v => v.PolyclinicId == polyclinicId && v.VisitDate == today && v.Status == VisitStatus.Waiting
                        && (doctorId == null || v.DoctorId == null || v.DoctorId == doctorId))
            .OrderBy(v => v.QueueNumber)
            .FirstOrDefaultAsync(ct);

        if (next is null)
        {
            return null;
        }

        next.StartExamination(doctorId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return next.Id;
    }

    /// <summary>
    /// Calls a waiting patient in. The physician is recorded only when they work in
    /// the visit's polyclinic (e.g. an administrator starting the visit is not).
    /// </summary>
    public async Task StartAsync(int id, int? doctorId, CancellationToken ct = default)
    {
        var doctor = doctorId is null
            ? null
            : await db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doctorId && d.IsActive, ct);
        await MutateAsync(id, v => v.StartExamination(doctor?.PolyclinicId == v.PolyclinicId ? doctor.Id : null, clock.UtcNow), ct);
    }

    public Task ReturnToQueueAsync(int id, CancellationToken ct = default)
        => MutateAsync(id, v => v.ReturnToQueue(), ct);

    public Task RecordExaminationAsync(int id, ExaminationInput input, CancellationToken ct = default)
        => MutateAsync(id, v => v.RecordExamination(input.Complaint, input.Diagnosis, input.Notes), ct);

    public Task CompleteAsync(int id, CancellationToken ct = default)
        => MutateAsync(id, v => v.Complete(clock.UtcNow), ct);

    public Task ReopenAsync(int id, CancellationToken ct = default)
        => MutateAsync(id, v => v.Reopen(), ct);

    public Task CancelAsync(int id, string? reason, CancellationToken ct = default)
        => MutateAsync(id, v => v.Cancel(reason, clock.UtcNow), ct);

    public async Task AssignDoctorAsync(int id, int doctorId, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doctorId && d.IsActive, ct)
            ?? throw NotFoundException.For("Hekim", doctorId);
        await MutateAsync(id, v => v.AssignDoctor(doctor), ct);
    }

    public async Task AddServiceAsync(int id, int serviceId, int quantity, CancellationToken ct = default)
    {
        var service = await db.MedicalServices.FirstOrDefaultAsync(s => s.Id == serviceId, ct)
            ?? throw NotFoundException.For("Hizmet", serviceId);
        await MutateAsync(id, v => v.AddService(service, quantity, clock.UtcNow), ct);
    }

    public Task RemoveServiceAsync(int id, int itemId, CancellationToken ct = default)
        => MutateAsync(id, v => v.RemoveService(itemId), ct);

    private async Task MutateAsync(int id, Action<Visit> action, CancellationToken ct)
    {
        var visit = await db.Visits
                        .Include(v => v.Items)
                        .Include(v => v.Payment)
                        .FirstOrDefaultAsync(v => v.Id == id, ct)
                    ?? throw NotFoundException.For("Başvuru", id);
        action(visit);
        await db.SaveChangesAsync(ct);
    }
}
