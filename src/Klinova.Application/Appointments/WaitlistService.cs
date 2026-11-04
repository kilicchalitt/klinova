using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Domain.Appointments;
using Klinova.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Klinova.Application.Appointments;

/// <summary>
/// Waiting list ("randevu bekleme listesi"): patients who found no free slot are
/// notified as soon as a matching appointment is cancelled.
/// </summary>
public sealed class WaitlistService(IAppDbContext db, IClock clock, IOptions<AppointmentPolicy> policy)
{
    private readonly AppointmentPolicy _policy = policy.Value;

    public async Task<int> JoinAsync(int patientId, int polyclinicId, int? doctorId, DateOnly earliest, DateOnly latest, CancellationToken ct = default)
    {
        var today = clock.Today;
        if (earliest < today)
        {
            earliest = today;
        }

        var lastDay = today.AddDays(_policy.BookingHorizonDays);
        if (latest > lastDay)
        {
            latest = lastDay;
        }

        var polyclinic = await db.Polyclinics.AsNoTracking().FirstOrDefaultAsync(p => p.Id == polyclinicId && p.IsActive, ct)
            ?? throw NotFoundException.For("Poliklinik", polyclinicId);
        if (doctorId is { } id && !await db.Doctors.AnyAsync(d => d.Id == id && d.PolyclinicId == polyclinicId && d.IsActive, ct))
        {
            throw new DomainException("Seçilen hekim bu poliklinikte görevli değil.");
        }

        var open = await db.WaitlistEntries.AsNoTracking()
            .Where(w => w.PatientId == patientId && (w.Status == WaitlistStatus.Active || w.Status == WaitlistStatus.Notified))
            .Select(w => w.PolyclinicId)
            .ToListAsync(ct);
        if (open.Contains(polyclinicId))
        {
            throw new DomainException($"{polyclinic.Name} için zaten bekleme listesindesiniz.");
        }

        if (open.Count >= _policy.MaxWaitlistEntriesPerPatient)
        {
            throw new DomainException($"Aynı anda en fazla {_policy.MaxWaitlistEntriesPerPatient} bekleme listesi kaydınız olabilir.");
        }

        var entry = new WaitlistEntry(patientId, polyclinicId, doctorId, earliest, latest);
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry.Id;
    }

    public async Task WithdrawAsync(int entryId, int? requestingPatientId = null, CancellationToken ct = default)
    {
        var entry = await db.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == entryId, ct)
            ?? throw NotFoundException.For("Bekleme kaydı", entryId);
        if (requestingPatientId is { } patientId && entry.PatientId != patientId)
        {
            throw NotFoundException.For("Bekleme kaydı", entryId);
        }

        entry.Withdraw();
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<WaitlistListItem>> ListAsync(int? patientId = null, bool openOnly = true, CancellationToken ct = default)
    {
        var entries = db.WaitlistEntries.AsNoTracking();
        if (patientId is { } id)
        {
            entries = entries.Where(w => w.PatientId == id);
        }

        if (openOnly)
        {
            entries = entries.Where(w => w.Status == WaitlistStatus.Active || w.Status == WaitlistStatus.Notified);
        }

        var rows = await entries
            .OrderBy(w => w.Id)
            .Select(w => new
            {
                w.Id,
                w.PatientId,
                w.Patient!.FirstName,
                w.Patient.LastName,
                w.Patient.Phone,
                w.PolyclinicId,
                PolyclinicName = w.Polyclinic!.Name,
                FacilityName = w.Polyclinic.Facility!.Name,
                DoctorTitle = w.Doctor != null ? w.Doctor.Title : null,
                DoctorFirst = w.Doctor != null ? w.Doctor.FirstName : null,
                DoctorLast = w.Doctor != null ? w.Doctor.LastName : null,
                w.EarliestDate,
                w.LatestDate,
                w.Status,
                w.CreatedAtUtc,
                w.NotifiedAtUtc,
            })
            .ToListAsync(ct);

        return rows.Select(w => new WaitlistListItem(
                w.Id, w.PatientId, $"{w.FirstName} {w.LastName}", w.Phone, w.PolyclinicId, w.PolyclinicName, w.FacilityName,
                w.DoctorFirst is null ? null : DoctorService.FullName(w.DoctorTitle, w.DoctorFirst, w.DoctorLast!),
                w.EarliestDate, w.LatestDate, w.Status, w.CreatedAtUtc, w.NotifiedAtUtc))
            .ToList();
    }
}
