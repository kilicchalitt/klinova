using Klinova.Application.Abstractions;
using Klinova.Application.Appointments;
using Klinova.Application.Organization;
using Klinova.Domain.Appointments;
using Klinova.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Klinova.Application.Scheduling;

public sealed record SlotView(int DoctorId, string DoctorName, DateOnly Date, TimeOnly Start, TimeOnly End);

/// <summary>Free appointment slots of a polyclinic or a physician.</summary>
public sealed class AvailabilityService(IAppDbContext db, IClock clock, IOptions<AppointmentPolicy> policy)
{
    private readonly AppointmentPolicy _policy = policy.Value;

    /// <summary>Earliest moment a slot may start for the given channel.</summary>
    public DateTime NotBeforeLocal(bool online)
        => clock.ToLocal(clock.UtcNow).AddMinutes(online ? _policy.MinimumLeadMinutes : 0);

    /// <summary>Last bookable day for the given channel.</summary>
    public DateOnly LastBookableDay(bool online)
        => clock.Today.AddDays(online ? _policy.BookingHorizonDays : _policy.StaffBookingHorizonDays);

    public async Task<IReadOnlyList<SlotView>> GetFreeSlotsAsync(int polyclinicId, int? doctorId, DateOnly from, DateOnly to, bool online, CancellationToken ct = default)
    {
        var today = clock.Today;
        if (from < today)
        {
            from = today;
        }

        var last = LastBookableDay(online);
        if (to > last)
        {
            to = last;
        }

        if (to < from)
        {
            return [];
        }

        var polyclinic = await db.Polyclinics.AsNoTracking().Include(p => p.Facility).FirstOrDefaultAsync(p => p.Id == polyclinicId, ct);
        if (polyclinic is null || !polyclinic.IsActive || !polyclinic.Facility!.IsActive || (online && !polyclinic.AcceptsOnlineAppointments))
        {
            return [];
        }

        var doctors = await db.Doctors.AsNoTracking()
            .Where(d => d.PolyclinicId == polyclinicId && d.IsActive && (doctorId == null || d.Id == doctorId))
            .Select(d => new { d.Id, d.Title, d.FirstName, d.LastName })
            .ToListAsync(ct);
        if (doctors.Count == 0)
        {
            return [];
        }

        var doctorIds = doctors.Select(d => d.Id).ToList();
        var schedules = await db.WorkSchedules.AsNoTracking()
            .Where(s => doctorIds.Contains(s.DoctorId) && s.ValidFrom <= to && (s.ValidUntil == null || s.ValidUntil >= from))
            .ToListAsync(ct);
        var leaves = await db.DoctorLeaves.AsNoTracking()
            .Where(l => doctorIds.Contains(l.DoctorId) && l.StartDate <= to && l.EndDate >= from)
            .ToListAsync(ct);
        var holidays = await db.Holidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).ToListAsync(ct);
        var booked = await db.Appointments.AsNoTracking()
            .Where(a => doctorIds.Contains(a.DoctorId) && a.Date >= from && a.Date <= to && a.Status != AppointmentStatus.Cancelled)
            .Select(a => new { a.DoctorId, a.Date, a.StartTime })
            .ToListAsync(ct);

        var slots = SlotCalculator.FreeSlots(schedules, leaves, holidays, booked.Select(b => (b.DoctorId, b.Date, b.StartTime)), from, to, NotBeforeLocal(online), online);
        var names = doctors.ToDictionary(d => d.Id, d => DoctorService.FullName(d.Title, d.FirstName, d.LastName));
        return slots.Select(s => new SlotView(s.DoctorId, names[s.DoctorId], s.Date, s.Start, s.End)).ToList();
    }

    /// <summary>The earliest free slot within the booking horizon, if any ("ilk boş randevu").</summary>
    public async Task<SlotView?> FindEarliestAsync(int polyclinicId, int? doctorId, bool online, CancellationToken ct = default)
    {
        var from = clock.Today;
        var last = LastBookableDay(online);
        while (from <= last)
        {
            var to = from.AddDays(13) < last ? from.AddDays(13) : last;
            var slots = await GetFreeSlotsAsync(polyclinicId, doctorId, from, to, online, ct);
            if (slots.Count > 0)
            {
                return slots[0];
            }

            from = to.AddDays(1);
        }

        return null;
    }
}
