using Klinova.Application.Abstractions;
using Klinova.Application.Notifications;
using Klinova.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Klinova.Application.Appointments;

public sealed record MaintenanceResult(int NoShows, int Reminders, int ExpiredWaitlistEntries);

/// <summary>
/// Periodic housekeeping run by a background worker: marks missed appointments as
/// no-show after their day has passed, queues reminders and expires old waiting-list requests.
/// </summary>
public sealed class AppointmentMaintenance(IAppDbContext db, IClock clock, NotificationService notifications, IOptions<AppointmentPolicy> policy, ILogger<AppointmentMaintenance> logger)
{
    private readonly AppointmentPolicy _policy = policy.Value;

    public async Task<MaintenanceResult> RunAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var nowLocal = clock.ToLocal(clock.UtcNow);

        var missed = await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Booked && a.Date < today)
            .ToListAsync(ct);
        foreach (var appointment in missed)
        {
            appointment.MarkNoShow(nowLocal);
        }

        var reminderLimit = nowLocal.AddHours(_policy.ReminderHoursBefore);
        var horizon = DateOnly.FromDateTime(reminderLimit);
        var upcoming = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.Polyclinic).ThenInclude(p => p!.Facility)
            .Where(a => a.Status == AppointmentStatus.Booked && a.ReminderSentAtUtc == null && a.Date >= today && a.Date <= horizon)
            .ToListAsync(ct);

        var reminders = 0;
        foreach (var appointment in upcoming.Where(a => a.StartsAtLocal > nowLocal && a.StartsAtLocal <= reminderLimit))
        {
            // Appointments booked inside the reminder window already received a confirmation.
            var bookedLocal = clock.ToLocal(appointment.CreatedAtUtc);
            if (appointment.StartsAtLocal - bookedLocal > TimeSpan.FromHours(_policy.ReminderHoursBefore))
            {
                await notifications.AppointmentReminderAsync(appointment.Patient!, appointment, appointment.Doctor!.FullName, appointment.Polyclinic!.Name, appointment.Polyclinic.Facility!.Name, ct);
                reminders++;
            }

            appointment.MarkReminderSent(clock.UtcNow);
        }

        var expired = await db.WaitlistEntries
            .Where(w => (w.Status == WaitlistStatus.Active || w.Status == WaitlistStatus.Notified) && w.LatestDate < today)
            .ToListAsync(ct);
        expired.ForEach(w => w.Expire());

        if (missed.Count > 0 || upcoming.Count > 0 || expired.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        if (missed.Count > 0 || reminders > 0 || expired.Count > 0)
        {
            logger.LogInformation("Appointment maintenance: {NoShows} no-shows, {Reminders} reminders, {Expired} expired waiting-list entries.", missed.Count, reminders, expired.Count);
        }

        return new MaintenanceResult(missed.Count, reminders, expired.Count);
    }
}
