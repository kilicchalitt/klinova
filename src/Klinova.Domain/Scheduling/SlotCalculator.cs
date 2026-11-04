namespace Klinova.Domain.Scheduling;

/// <summary>
/// Expands weekly work schedules into concrete free slots, removing leave days,
/// holidays, slots that are already booked and slots that start too early.
/// </summary>
public static class SlotCalculator
{
    public static IReadOnlyList<Slot> FreeSlots(
        IEnumerable<WorkSchedule> schedules,
        IEnumerable<DoctorLeave> leaves,
        IEnumerable<Holiday> holidays,
        IEnumerable<(int DoctorId, DateOnly Date, TimeOnly Start)> booked,
        DateOnly from,
        DateOnly to,
        DateTime notBeforeLocal,
        bool onlineOnly)
    {
        var scheduleList = schedules.ToList();
        var leaveList = leaves.ToList();
        var holidayList = holidays.ToList();
        var bookedSet = booked.ToHashSet();
        var result = new List<Slot>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            foreach (var schedule in scheduleList.Where(s => s.AppliesTo(date)).OrderBy(s => s.StartTime))
            {
                if (onlineOnly && !schedule.IsOnlineBookable)
                {
                    continue;
                }

                if (leaveList.Any(l => l.DoctorId == schedule.DoctorId && l.Covers(date)))
                {
                    continue;
                }

                var first = (int)schedule.StartTime.ToTimeSpan().TotalMinutes;
                var last = (int)schedule.EndTime.ToTimeSpan().TotalMinutes;
                for (var minute = first; minute + schedule.SlotMinutes <= last; minute += schedule.SlotMinutes)
                {
                    var start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute));
                    if (date.ToDateTime(start) < notBeforeLocal
                        || holidayList.Any(h => h.Closes(date, start))
                        || bookedSet.Contains((schedule.DoctorId, date, start)))
                    {
                        continue;
                    }

                    result.Add(new Slot(schedule.DoctorId, date, start, start.AddMinutes(schedule.SlotMinutes), schedule.IsOnlineBookable));
                }
            }
        }

        return result
            .OrderBy(s => s.Date).ThenBy(s => s.Start).ThenBy(s => s.DoctorId)
            .ToList();
    }
}
