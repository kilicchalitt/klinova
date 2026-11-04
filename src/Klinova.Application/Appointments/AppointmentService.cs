using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Application.Notifications;
using Klinova.Application.Organization;
using Klinova.Application.Scheduling;
using Klinova.Application.Visits;
using Klinova.Domain.Appointments;
using Klinova.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Klinova.Application.Appointments;

/// <summary>
/// Appointment booking, rescheduling, cancellation, check-in and no-show handling.
/// Online bookings (patient portal) follow the institution's <see cref="AppointmentPolicy"/>;
/// staff bookings are allowed further ahead and are not subject to online-only limits.
/// </summary>
public sealed class AppointmentService(
    IAppDbContext db,
    IClock clock,
    AvailabilityService availability,
    NotificationService notifications,
    VisitService visits,
    IOptions<AppointmentPolicy> policy)
{
    private readonly AppointmentPolicy _policy = policy.Value;

    public async Task<Appointment> BookAsync(BookingRequest request, AppointmentChannel channel, CancellationToken ct = default)
    {
        var appointment = await PrepareAsync(request, channel, replacing: null, ct);
        await SaveBookingAsync(ct);
        return appointment;
    }

    /// <summary>Moves an active appointment to another slot; the old one is cancelled in the same transaction.</summary>
    public async Task<Appointment> RescheduleAsync(int appointmentId, BookingRequest request, AppointmentChannel channel, int? requestingPatientId = null, CancellationToken ct = default)
    {
        var existing = await db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw NotFoundException.For("Randevu", appointmentId);
        EnsureOwner(existing, requestingPatientId);
        if (requestingPatientId is not null)
        {
            EnsureBeforeCancellationDeadline(existing);
        }

        request.PatientId = existing.PatientId;
        var replacement = await PrepareAsync(request, channel, replacing: existing, ct);
        existing.Cancel(requestingPatientId is null ? CancellationSource.Staff : CancellationSource.Patient, "Randevu değiştirildi.", clock.UtcNow);
        await NotifyWaitlistAsync(existing, ct);
        await SaveBookingAsync(ct);

        replacement.LinkToPrevious(existing.Id);
        await db.SaveChangesAsync(ct);
        return replacement;
    }

    public async Task CancelAsync(int appointmentId, CancellationSource source, string? reason, int? requestingPatientId = null, CancellationToken ct = default)
    {
        var appointment = await db.Appointments
                              .Include(a => a.Patient)
                              .Include(a => a.Polyclinic)
                              .FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
                          ?? throw NotFoundException.For("Randevu", appointmentId);
        EnsureOwner(appointment, requestingPatientId);

        if (source == CancellationSource.Patient)
        {
            EnsureBeforeCancellationDeadline(appointment);
        }

        appointment.Cancel(source, reason, clock.UtcNow);
        await notifications.AppointmentCancelledAsync(appointment.Patient!, appointment, appointment.Polyclinic!.Name, ct);
        await NotifyWaitlistAsync(appointment, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Registers the arrival of the patient and opens a visit with a queue number.</summary>
    public async Task<int> CheckInAsync(int appointmentId, CancellationToken ct = default)
    {
        var snapshot = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw NotFoundException.For("Randevu", appointmentId);
        if (snapshot.Status != AppointmentStatus.Booked)
        {
            throw new DomainException("Yalnızca aktif randevular için hasta kabulü yapılabilir.");
        }

        if (snapshot.Date != clock.Today)
        {
            throw new DomainException("Randevu kabulü yalnızca randevu gününde yapılabilir.");
        }

        var visit = await visits.AdmitAsync(new AdmissionInput
        {
            PatientId = snapshot.PatientId,
            PolyclinicId = snapshot.PolyclinicId,
            DoctorId = snapshot.DoctorId,
            Complaint = snapshot.PatientNote,
            ServiceIds = await db.MedicalServices.Where(s => s.Code == "MUA" && s.IsActive).Select(s => s.Id).ToListAsync(ct),
        }, ct);

        // Load after admission: the admission may reset the change tracker when it retries a queue number.
        var appointment = await db.Appointments.FirstAsync(a => a.Id == appointmentId, ct);
        appointment.CheckIn(visit.Id, clock.Today, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return visit.Id;
    }

    public async Task MarkNoShowAsync(int appointmentId, CancellationToken ct = default)
    {
        var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw NotFoundException.For("Randevu", appointmentId);
        appointment.MarkNoShow(clock.ToLocal(clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task<Appointment> GetAsync(int id, CancellationToken ct = default)
        => await db.Appointments.AsNoTracking()
               .Include(a => a.Patient)
               .Include(a => a.Doctor)
               .Include(a => a.Polyclinic).ThenInclude(p => p!.Facility)
               .FirstOrDefaultAsync(a => a.Id == id, ct)
           ?? throw NotFoundException.For("Randevu", id);

    public Task<Appointment?> FindByVisitAsync(int visitId, CancellationToken ct = default)
        => db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.VisitId == visitId, ct);

    public async Task<List<AppointmentListItem>> ListAsync(AppointmentQuery query, CancellationToken ct = default)
    {
        var appointments = db.Appointments.AsNoTracking();
        if (query.Date is { } date)
        {
            appointments = appointments.Where(a => a.Date == date);
        }

        if (query.FacilityId is { } facilityId)
        {
            appointments = appointments.Where(a => a.Polyclinic!.FacilityId == facilityId);
        }

        if (query.PolyclinicId is { } polyclinicId)
        {
            appointments = appointments.Where(a => a.PolyclinicId == polyclinicId);
        }

        if (query.DoctorId is { } doctorId)
        {
            appointments = appointments.Where(a => a.DoctorId == doctorId);
        }

        if (query.Status is { } status)
        {
            appointments = appointments.Where(a => a.Status == status);
        }

        return await ProjectAsync(appointments, ct);
    }

    public async Task<List<AppointmentListItem>> ListForPatientAsync(int patientId, CancellationToken ct = default)
        => (await ProjectAsync(db.Appointments.AsNoTracking().Where(a => a.PatientId == patientId), ct))
            .OrderByDescending(a => a.Date).ThenByDescending(a => a.StartTime).ToList();

    /// <summary>
    /// MHRS-style restriction: a no-show blocks online booking in that polyclinic for
    /// <see cref="AppointmentPolicy.NoShowRestrictionDays"/>; repeated no-shows block all polyclinics.
    /// </summary>
    public async Task<BookingRestriction?> GetRestrictionAsync(int patientId, int polyclinicId, CancellationToken ct = default)
    {
        if (_policy.NoShowRestrictionDays <= 0)
        {
            return null;
        }

        var today = clock.Today;
        var since = today.AddDays(-_policy.NoShowRestrictionDays);
        var noShows = await db.Appointments.AsNoTracking()
            .Where(a => a.PatientId == patientId && a.Status == AppointmentStatus.NoShow && a.Date > since)
            .Select(a => new { a.Date, a.PolyclinicId })
            .ToListAsync(ct);

        if (_policy.NoShowsForGlobalRestriction > 0 && noShows.Count >= _policy.NoShowsForGlobalRestriction)
        {
            return new BookingRestriction(noShows.Max(n => n.Date).AddDays(_policy.NoShowRestrictionDays), AllPolyclinics: true);
        }

        var inPolyclinic = noShows.Where(n => n.PolyclinicId == polyclinicId).ToList();
        return inPolyclinic.Count == 0
            ? null
            : new BookingRestriction(inPolyclinic.Max(n => n.Date).AddDays(_policy.NoShowRestrictionDays), AllPolyclinics: false);
    }

    public bool CanPatientCancel(AppointmentListItem appointment)
        => appointment.Status == AppointmentStatus.Booked
           && appointment.StartsAtLocal - clock.ToLocal(clock.UtcNow) >= TimeSpan.FromHours(_policy.CancellationDeadlineHours);

    private async Task<Appointment> PrepareAsync(BookingRequest request, AppointmentChannel channel, Appointment? replacing, CancellationToken ct)
    {
        var online = channel == AppointmentChannel.Online;
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId, ct)
            ?? throw NotFoundException.For("Hasta", request.PatientId);
        if (patient.IsArchived)
        {
            throw new DomainException("Arşivlenmiş hasta için randevu verilemez.");
        }

        var doctor = await db.Doctors.AsNoTracking()
                         .Include(d => d.Polyclinic).ThenInclude(p => p!.Facility)
                         .FirstOrDefaultAsync(d => d.Id == request.DoctorId, ct)
                     ?? throw NotFoundException.For("Hekim", request.DoctorId);
        var polyclinic = doctor.Polyclinic!;

        var slots = await availability.GetFreeSlotsAsync(polyclinic.Id, doctor.Id, request.Date, request.Date, online, ct);
        var slot = slots.FirstOrDefault(s => s.Start == request.StartTime)
            ?? throw new DomainException("Seçilen saat artık uygun değil. Lütfen başka bir saat seçin.");

        var today = clock.Today;
        var active = await db.Appointments.AsNoTracking()
            .Where(a => a.PatientId == patient.Id && a.Status == AppointmentStatus.Booked && a.Date >= today)
            .Select(a => new { a.Id, a.PolyclinicId, a.Date, a.StartTime, a.EndTime })
            .ToListAsync(ct);
        if (replacing is not null)
        {
            active = active.Where(a => a.Id != replacing.Id).ToList();
        }

        if (active.Count(a => a.PolyclinicId == polyclinic.Id) >= _policy.MaxActivePerPolyclinic)
        {
            throw new DomainException($"{polyclinic.Name} polikliniğinde zaten aktif bir randevunuz var. Yeni randevu almak için önce mevcut randevuyu iptal edin veya değiştirin.");
        }

        if (active.Any(a => a.Date == slot.Date && a.StartTime < slot.End && slot.Start < a.EndTime))
        {
            throw new DomainException("Aynı saatte başka bir randevunuz bulunuyor.");
        }

        if (online)
        {
            if (active.Count >= _policy.MaxActivePerPatient)
            {
                throw new DomainException($"Aynı anda en fazla {_policy.MaxActivePerPatient} aktif randevunuz olabilir.");
            }

            if (await GetRestrictionAsync(patient.Id, polyclinic.Id, ct) is { } restriction)
            {
                throw new DomainException(restriction.AllPolyclinics
                    ? $"Gitmediğiniz randevular nedeniyle {restriction.Until.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture)} tarihine kadar online randevu alamazsınız."
                    : $"Gitmediğiniz bir randevu nedeniyle {restriction.Until.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture)} tarihine kadar {polyclinic.Name} polikliniğinden online randevu alamazsınız.");
            }
        }

        var appointment = new Appointment(patient.Id, doctor.Id, polyclinic.Id, slot.Date, slot.Start, slot.End, channel, request.Note);
        db.Appointments.Add(appointment);

        var waiting = await db.WaitlistEntries
            .Where(w => w.PatientId == patient.Id && w.PolyclinicId == polyclinic.Id && (w.Status == WaitlistStatus.Active || w.Status == WaitlistStatus.Notified))
            .ToListAsync(ct);
        waiting.ForEach(w => w.MarkFulfilled());

        await notifications.AppointmentBookedAsync(patient, appointment, doctor.FullName, polyclinic.Name, polyclinic.Facility!.Name, ct);
        return appointment;
    }

    private async Task SaveBookingAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // IX_Appointments_ActiveSlot: someone else took the slot a moment ago.
            db.DiscardChanges();
            throw new DomainException("Seçtiğiniz saat az önce başka bir hasta tarafından alındı. Lütfen başka bir saat seçin.", ex);
        }
    }

    private async Task NotifyWaitlistAsync(Appointment freed, CancellationToken ct)
    {
        if (freed.StartsAtLocal <= clock.ToLocal(clock.UtcNow) || _policy.WaitlistNotifyCount <= 0)
        {
            return;
        }

        var candidates = await db.WaitlistEntries
            .Include(w => w.Patient)
            .Where(w => w.PolyclinicId == freed.PolyclinicId && w.Status == WaitlistStatus.Active && w.PatientId != freed.PatientId
                        && w.EarliestDate <= freed.Date && w.LatestDate >= freed.Date
                        && (w.DoctorId == null || w.DoctorId == freed.DoctorId))
            .OrderBy(w => w.Id)
            .Take(_policy.WaitlistNotifyCount)
            .ToListAsync(ct);

        var polyclinicName = await db.Polyclinics.Where(p => p.Id == freed.PolyclinicId).Select(p => p.Name).FirstAsync(ct);
        foreach (var entry in candidates)
        {
            entry.MarkNotified(clock.UtcNow);
            await notifications.WaitlistSlotAvailableAsync(entry.Patient!, polyclinicName, freed.Date, freed.StartTime, ct);
        }
    }

    private void EnsureBeforeCancellationDeadline(Appointment appointment)
    {
        if (appointment.StartsAtLocal - clock.ToLocal(clock.UtcNow) < TimeSpan.FromHours(_policy.CancellationDeadlineHours))
        {
            throw new DomainException($"Randevunuza {_policy.CancellationDeadlineHours} saatten az kaldığı için online işlem yapılamaz. Lütfen kurumu telefonla arayın.");
        }
    }

    private static void EnsureOwner(Appointment appointment, int? requestingPatientId)
    {
        if (requestingPatientId is { } patientId && appointment.PatientId != patientId)
        {
            throw NotFoundException.For("Randevu", appointment.Id);
        }
    }

    private static async Task<List<AppointmentListItem>> ProjectAsync(IQueryable<Appointment> appointments, CancellationToken ct)
    {
        var rows = await appointments
            .Select(a => new
            {
                a.Id,
                a.Date,
                a.StartTime,
                a.EndTime,
                a.PatientId,
                PatientFirst = a.Patient!.FirstName,
                PatientLast = a.Patient.LastName,
                a.Patient.Phone,
                a.DoctorId,
                a.Doctor!.Title,
                DoctorFirst = a.Doctor.FirstName,
                DoctorLast = a.Doctor.LastName,
                a.PolyclinicId,
                PolyclinicName = a.Polyclinic!.Name,
                FacilityName = a.Polyclinic.Facility!.Name,
                a.Status,
                a.Channel,
                a.ConfirmationCode,
                a.PatientNote,
                a.VisitId,
            })
            .ToListAsync(ct);

        return rows
            .OrderBy(a => a.Date).ThenBy(a => a.StartTime).ThenBy(a => a.DoctorLast, TurkishCollation.Comparer)
            .Select(a => new AppointmentListItem(
                a.Id, a.Date, a.StartTime, a.EndTime, a.PatientId, $"{a.PatientFirst} {a.PatientLast}", a.Phone,
                a.DoctorId, DoctorService.FullName(a.Title, a.DoctorFirst, a.DoctorLast), a.PolyclinicId, a.PolyclinicName, a.FacilityName,
                a.Status, a.Channel, a.ConfirmationCode, a.PatientNote, a.VisitId))
            .ToList();
    }
}
