using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Scheduling;

public sealed class WorkScheduleInput
{
    [Display(Name = "Gün")]
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    [Display(Name = "Başlangıç")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    public TimeOnly StartTime { get; set; } = new(9, 0);

    [Display(Name = "Bitiş")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    public TimeOnly EndTime { get; set; } = new(12, 0);

    [Display(Name = "Randevu Süresi (dk)")]
    [Range(WorkSchedule.MinSlotMinutes, WorkSchedule.MaxSlotMinutes, ErrorMessage = "{0} {1}-{2} dakika olmalıdır.")]
    public int SlotMinutes { get; set; } = 15;

    [Display(Name = "Geçerlilik Başlangıcı")]
    public DateOnly ValidFrom { get; set; }

    [Display(Name = "Geçerlilik Bitişi")]
    public DateOnly? ValidUntil { get; set; }

    [Display(Name = "Online randevuya açık")]
    public bool IsOnlineBookable { get; set; } = true;

    /// <summary>Optional: create the same block for every weekday (Monday–Friday).</summary>
    [Display(Name = "Hafta içi her güne uygula")]
    public bool ApplyToWeekdays { get; set; }
}

public sealed class LeaveInput
{
    [Display(Name = "Başlangıç")]
    public DateOnly StartDate { get; set; }

    [Display(Name = "Bitiş")]
    public DateOnly EndDate { get; set; }

    [Display(Name = "Açıklama")]
    [StringLength(FieldLengths.ShortText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Reason { get; set; }
}

public sealed class HolidayInput
{
    [Display(Name = "Tarih")]
    public DateOnly Date { get; set; }

    [Display(Name = "Ad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.ShortText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Yarım gün (bu saatten itibaren kapalı)")]
    public TimeOnly? ClosedFrom { get; set; }
}

public sealed record DoctorScheduleView(
    int DoctorId,
    string DoctorName,
    string PolyclinicName,
    IReadOnlyList<WorkSchedule> Schedules,
    IReadOnlyList<DoctorLeave> Leaves);

/// <summary>Physician working hours ("çalışma cetveli"), leave and holidays.</summary>
public sealed class ScheduleService(IAppDbContext db, IClock clock)
{
    public async Task<DoctorScheduleView> GetDoctorScheduleAsync(int doctorId, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.AsNoTracking().Include(d => d.Polyclinic).FirstOrDefaultAsync(d => d.Id == doctorId, ct)
            ?? throw NotFoundException.For("Hekim", doctorId);
        var schedules = await db.WorkSchedules.AsNoTracking().Where(s => s.DoctorId == doctorId).ToListAsync(ct);
        var today = clock.Today;
        var leaves = await db.DoctorLeaves.AsNoTracking().Where(l => l.DoctorId == doctorId && l.EndDate >= today.AddDays(-30)).OrderBy(l => l.StartDate).ToListAsync(ct);

        return new DoctorScheduleView(
            doctor.Id,
            doctor.FullName,
            doctor.Polyclinic!.Name,
            schedules.OrderBy(s => ((int)s.DayOfWeek + 6) % 7).ThenBy(s => s.StartTime).ToList(),
            leaves);
    }

    public async Task<int> AddScheduleAsync(int doctorId, WorkScheduleInput input, CancellationToken ct = default)
    {
        if (!await db.Doctors.AnyAsync(d => d.Id == doctorId, ct))
        {
            throw NotFoundException.For("Hekim", doctorId);
        }

        var validFrom = input.ValidFrom == default ? clock.Today : input.ValidFrom;
        DayOfWeek[] days = input.ApplyToWeekdays
            ? [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]
            : [input.DayOfWeek];

        var existing = await db.WorkSchedules.AsNoTracking().Where(s => s.DoctorId == doctorId).ToListAsync(ct);
        var created = new List<WorkSchedule>();
        foreach (var day in days)
        {
            var schedule = new WorkSchedule(doctorId, day, input.StartTime, input.EndTime, input.SlotMinutes, validFrom, input.ValidUntil, input.IsOnlineBookable);
            if (existing.Concat(created).Any(e => e.Overlaps(schedule)))
            {
                throw new DomainException($"{DayName(day)} günü için bu saat aralığı mevcut bir çalışma bloğuyla çakışıyor.");
            }

            created.Add(schedule);
        }

        db.WorkSchedules.AddRange(created);
        await db.SaveChangesAsync(ct);
        return created.Count;
    }

    public async Task DeleteScheduleAsync(int scheduleId, CancellationToken ct = default)
    {
        var schedule = await db.WorkSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct)
            ?? throw NotFoundException.For("Çalışma bloğu", scheduleId);
        db.WorkSchedules.Remove(schedule);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Adds leave and reports how many active appointments fall into it.</summary>
    public async Task<int> AddLeaveAsync(int doctorId, LeaveInput input, CancellationToken ct = default)
    {
        if (!await db.Doctors.AnyAsync(d => d.Id == doctorId, ct))
        {
            throw NotFoundException.For("Hekim", doctorId);
        }

        db.DoctorLeaves.Add(new DoctorLeave(doctorId, input.StartDate, input.EndDate, input.Reason));
        await db.SaveChangesAsync(ct);

        return await db.Appointments.CountAsync(
            a => a.DoctorId == doctorId && a.Status == Domain.Appointments.AppointmentStatus.Booked && a.Date >= input.StartDate && a.Date <= input.EndDate,
            ct);
    }

    public async Task DeleteLeaveAsync(int leaveId, CancellationToken ct = default)
    {
        var leave = await db.DoctorLeaves.FirstOrDefaultAsync(l => l.Id == leaveId, ct)
            ?? throw NotFoundException.For("İzin", leaveId);
        db.DoctorLeaves.Remove(leave);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<Holiday>> ListHolidaysAsync(int year, CancellationToken ct = default)
    {
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        return db.Holidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date).ToListAsync(ct);
    }

    public async Task AddHolidayAsync(HolidayInput input, CancellationToken ct = default)
    {
        if (await db.Holidays.AnyAsync(h => h.Date == input.Date, ct))
        {
            throw new DomainException("Bu tarih için zaten bir tatil tanımlı.");
        }

        db.Holidays.Add(new Holiday { Date = input.Date, Name = Text.Required(input.Name), ClosedFrom = input.ClosedFrom });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteHolidayAsync(int id, CancellationToken ct = default)
    {
        var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == id, ct) ?? throw NotFoundException.For("Tatil", id);
        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync(ct);
    }

    public static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        _ => "Pazar",
    };
}
