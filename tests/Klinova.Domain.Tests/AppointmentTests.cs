using Klinova.Domain.Appointments;
using Klinova.Domain.Common;

namespace Klinova.Domain.Tests;

public class AppointmentTests
{
    private static readonly DateOnly Day = new(2026, 11, 9);
    private static readonly DateTime Now = new(2026, 11, 9, 6, 0, 0, DateTimeKind.Utc);

    private static Appointment New() => new(1, 2, 3, Day, new TimeOnly(10, 0), new TimeOnly(10, 15), AppointmentChannel.Online, "  Kontrol için  ");

    [Fact]
    public void NewAppointment_IsActiveWithConfirmationCode()
    {
        var a = New();

        Assert.Equal(AppointmentStatus.Booked, a.Status);
        Assert.True(a.IsActive);
        Assert.Equal(8, a.ConfirmationCode.Length);
        Assert.DoesNotContain(a.ConfirmationCode, c => c is 'O' or '0' or 'I' or '1');
        Assert.Equal("Kontrol için", a.PatientNote);
    }

    [Fact]
    public void ConfirmationCodes_AreRandom()
        => Assert.Equal(50, Enumerable.Range(0, 50).Select(_ => New().ConfirmationCode).Distinct().Count());

    [Fact]
    public void Cancel_RecordsSourceAndReason()
    {
        var a = New();

        a.Cancel(CancellationSource.Patient, "Gelemeyeceğim", Now);

        Assert.Equal(AppointmentStatus.Cancelled, a.Status);
        Assert.Equal(CancellationSource.Patient, a.CancelledBy);
        Assert.Equal(Now, a.CancelledAtUtc);
        Assert.Throws<DomainException>(() => a.Cancel(CancellationSource.Staff, null, Now));
    }

    [Fact]
    public void CheckIn_OnlyOnTheAppointmentDay()
    {
        var a = New();

        Assert.Throws<DomainException>(() => a.CheckIn(5, Day.AddDays(-1), Now));
        a.CheckIn(5, Day, Now);

        Assert.Equal(AppointmentStatus.CheckedIn, a.Status);
        Assert.Equal(5, a.VisitId);
    }

    [Fact]
    public void MarkNoShow_NotBeforeTheAppointmentTime()
    {
        var a = New();

        Assert.Throws<DomainException>(() => a.MarkNoShow(Day.ToDateTime(new TimeOnly(9, 0))));
        a.MarkNoShow(Day.ToDateTime(new TimeOnly(11, 0)));

        Assert.Equal(AppointmentStatus.NoShow, a.Status);
    }

    [Fact]
    public void NoteLongerThanLimit_IsRejected()
        => Assert.Throws<DomainException>(() => new Appointment(1, 2, 3, Day, new TimeOnly(10, 0), new TimeOnly(10, 15), AppointmentChannel.Desk, new string('x', Appointment.NoteMaxLength + 1)));

    [Fact]
    public void Waitlist_MatchesPolyclinicDoctorAndDateRange()
    {
        var anyDoctor = new WaitlistEntry(1, 3, null, Day, Day.AddDays(7));
        var specific = new WaitlistEntry(1, 3, 2, Day, Day.AddDays(7));

        Assert.True(anyDoctor.Matches(3, 99, Day.AddDays(3)));
        Assert.False(anyDoctor.Matches(4, 99, Day));
        Assert.False(anyDoctor.Matches(3, 99, Day.AddDays(8)));
        Assert.True(specific.Matches(3, 2, Day));
        Assert.False(specific.Matches(3, 5, Day));

        specific.Withdraw();
        Assert.False(specific.Matches(3, 2, Day));
    }
}
