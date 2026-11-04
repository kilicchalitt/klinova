using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Visits;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Dashboard;

public sealed record PolyclinicLoad(int PolyclinicId, string Name, int Waiting, int InExamination, int Completed);

public sealed record DashboardSummary(
    DateOnly Day,
    int TotalVisits,
    int Waiting,
    int InExamination,
    int Completed,
    int Cancelled,
    int NewPatients,
    int OutstandingPayments,
    decimal CollectedToday,
    IReadOnlyList<PolyclinicLoad> Polyclinics,
    int AppointmentsToday = 0,
    int AppointmentsCheckedIn = 0,
    int AppointmentsWaiting = 0);

/// <summary>Figures for the start page: today's load and revenue at a glance.</summary>
public sealed class DashboardService(IAppDbContext db, IClock clock)
{
    public async Task<DashboardSummary> GetAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var startUtc = clock.StartOfDayUtc(today);
        var endUtc = clock.StartOfDayUtc(today.AddDays(1));

        var todays = await db.Visits.AsNoTracking()
            .Where(v => v.VisitDate == today)
            .GroupBy(v => new { v.PolyclinicId, v.Status })
            .Select(g => new { g.Key.PolyclinicId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var polyclinics = (await db.Polyclinics.AsNoTracking()
                .Where(p => p.IsActive)
                .Select(p => new { p.Id, p.Name, Facility = p.Facility!.Name })
                .ToListAsync(ct))
            .SortBy(p => p.Facility + " " + p.Name);
        var multipleFacilities = polyclinics.Select(p => p.Facility).Distinct().Count() > 1;

        var loads = polyclinics
            .Select(p => new PolyclinicLoad(
                p.Id,
                multipleFacilities ? $"{p.Name} — {p.Facility}" : p.Name,
                todays.Where(t => t.PolyclinicId == p.Id && t.Status == VisitStatus.Waiting).Sum(t => t.Count),
                todays.Where(t => t.PolyclinicId == p.Id && t.Status == VisitStatus.InExamination).Sum(t => t.Count),
                todays.Where(t => t.PolyclinicId == p.Id && t.Status == VisitStatus.Completed).Sum(t => t.Count)))
            .ToList();

        var newPatients = await db.Patients.CountAsync(p => p.CreatedAtUtc >= startUtc && p.CreatedAtUtc < endUtc, ct);

        var outstanding = await db.Visits.CountAsync(
            v => v.Status == VisitStatus.Completed && v.Payment == null && v.Items.Any(), ct);

        var collected = (await db.Payments.AsNoTracking()
                .Where(p => p.PaidAtUtc >= startUtc && p.PaidAtUtc < endUtc)
                .Select(p => p.NetAmount)
                .ToListAsync(ct))
            .Sum();

        int Count(VisitStatus s) => todays.Where(t => t.Status == s).Sum(t => t.Count);

        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.Date == today && a.Status != Domain.Appointments.AppointmentStatus.Cancelled)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new DashboardSummary(
            today,
            todays.Sum(t => t.Count),
            Count(VisitStatus.Waiting),
            Count(VisitStatus.InExamination),
            Count(VisitStatus.Completed),
            Count(VisitStatus.Cancelled),
            newPatients,
            outstanding,
            collected,
            loads,
            appointments.Sum(a => a.Count),
            appointments.Where(a => a.Status == Domain.Appointments.AppointmentStatus.CheckedIn).Sum(a => a.Count),
            appointments.Where(a => a.Status == Domain.Appointments.AppointmentStatus.Booked).Sum(a => a.Count));
    }
}
