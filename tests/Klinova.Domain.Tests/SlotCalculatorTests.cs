using Klinova.Domain.Common;
using Klinova.Domain.Scheduling;

namespace Klinova.Domain.Tests;

public class SlotCalculatorTests
{
    // 2026-11-09 is a Monday.
    private static readonly DateOnly Monday = new(2026, 11, 9);
    private static readonly DateTime LongAgo = new(2000, 1, 1);

    private static WorkSchedule MondayMorning(int doctorId = 1, int slot = 30, bool online = true)
        => new(doctorId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0), slot, new DateOnly(2026, 1, 1), null, online);

    private static IReadOnlyList<Slot> Calculate(
        IEnumerable<WorkSchedule> schedules,
        IEnumerable<DoctorLeave>? leaves = null,
        IEnumerable<Holiday>? holidays = null,
        IEnumerable<(int, DateOnly, TimeOnly)>? booked = null,
        DateTime? notBefore = null,
        bool onlineOnly = false,
        DateOnly? to = null)
        => SlotCalculator.FreeSlots(schedules, leaves ?? [], holidays ?? [], booked ?? [], Monday, to ?? Monday, notBefore ?? LongAgo, onlineOnly);

    [Fact]
    public void GeneratesSlotsWithinTheWorkingBlock()
    {
        var slots = Calculate([MondayMorning()]);

        Assert.Equal(["09:00", "09:30", "10:00", "10:30"], slots.Select(s => s.Start.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.All(slots, s => Assert.Equal(s.Start.AddMinutes(30), s.End));
    }

    [Fact]
    public void DoesNotCreateAPartialSlotAtTheEnd()
    {
        var schedule = new WorkSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0), 25, new DateOnly(2026, 1, 1), null, true);

        Assert.Equal(2, Calculate([schedule]).Count);
    }

    [Fact]
    public void SkipsOtherWeekdays()
        => Assert.DoesNotContain(Calculate([MondayMorning()], to: Monday.AddDays(5)), s => s.Date != Monday);

    [Fact]
    public void RespectsValidityPeriod()
    {
        var expired = new WorkSchedule(1, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0), 30, new DateOnly(2026, 1, 1), new DateOnly(2026, 11, 1), true);

        Assert.Empty(Calculate([expired]));
    }

    [Fact]
    public void RemovesBookedSlots()
    {
        var slots = Calculate([MondayMorning()], booked: [(1, Monday, new TimeOnly(9, 30))]);

        Assert.DoesNotContain(slots, s => s.Start == new TimeOnly(9, 30));
        Assert.Equal(3, slots.Count);
    }

    [Fact]
    public void RemovesLeaveDays()
        => Assert.Empty(Calculate([MondayMorning()], leaves: [new DoctorLeave(1, Monday, Monday.AddDays(2), "Kongre")]));

    [Fact]
    public void LeaveOfAnotherDoctorDoesNotMatter()
        => Assert.Equal(4, Calculate([MondayMorning()], leaves: [new DoctorLeave(2, Monday, Monday, null)]).Count);

    [Fact]
    public void FullDayHolidayClosesAllSlots()
        => Assert.Empty(Calculate([MondayMorning()], holidays: [new Holiday { Date = Monday, Name = "Bayram" }]));

    [Fact]
    public void HalfDayHolidayClosesSlotsFromItsStartTime()
    {
        var slots = Calculate([MondayMorning()], holidays: [new Holiday { Date = Monday, Name = "Arife", ClosedFrom = new TimeOnly(10, 0) }]);

        Assert.Equal([new TimeOnly(9, 0), new TimeOnly(9, 30)], slots.Select(s => s.Start));
    }

    [Fact]
    public void RemovesSlotsBeforeTheEarliestAllowedTime()
    {
        var slots = Calculate([MondayMorning()], notBefore: Monday.ToDateTime(new TimeOnly(9, 45)));

        Assert.Equal([new TimeOnly(10, 0), new TimeOnly(10, 30)], slots.Select(s => s.Start));
    }

    [Fact]
    public void OnlineOnlyExcludesDeskOnlyBlocks()
    {
        var deskOnly = MondayMorning(online: false);

        Assert.Empty(Calculate([deskOnly], onlineOnly: true));
        Assert.Equal(4, Calculate([deskOnly], onlineOnly: false).Count);
    }

    [Fact]
    public void MergesSeveralDoctorsOrderedByTime()
    {
        var slots = Calculate([MondayMorning(doctorId: 2), MondayMorning(doctorId: 1)]);

        Assert.Equal(8, slots.Count);
        Assert.Equal(1, slots[0].DoctorId);
        Assert.Equal(2, slots[1].DoctorId);
    }

    [Theory]
    [InlineData(10, 9, 30)]   // end before start
    [InlineData(9, 10, 3)]    // slot too short
    [InlineData(9, 10, 300)]  // slot too long
    [InlineData(9, 9, 30)]    // empty block
    public void WorkSchedule_RejectsInvalidBlocks(int startHour, int endHour, int slot)
        => Assert.Throws<DomainException>(() => new WorkSchedule(1, DayOfWeek.Monday, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0), slot, Monday, null, true));

    [Fact]
    public void WorkSchedule_DetectsOverlaps()
    {
        var morning = MondayMorning();
        var overlapping = new WorkSchedule(1, DayOfWeek.Monday, new TimeOnly(10, 30), new TimeOnly(12, 0), 30, Monday, null, true);
        var afternoon = new WorkSchedule(1, DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(12, 0), 30, Monday, null, true);

        Assert.True(morning.Overlaps(overlapping));
        Assert.False(morning.Overlaps(afternoon));
    }
}
