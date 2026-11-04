using Klinova.Application.Appointments;
using Klinova.Application.Notifications;
using Klinova.Application.Scheduling;
using Klinova.Application.Visits;
using Klinova.Domain.Appointments;
using Klinova.Domain.Common;
using Klinova.Domain.Notifications;
using Klinova.Domain.Organization;
using Klinova.Domain.Patients;
using Klinova.Domain.Scheduling;
using Klinova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Klinova.Application.Tests;

/// <summary>
/// Booking rules. The fake clock stands at Wednesday 2026-11-04 10:00 Istanbul time; the
/// physician works on weekdays 09:00–12:00 in 30-minute slots.
/// </summary>
public sealed class AppointmentTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 4);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private readonly TestDatabase _db = new();
    private readonly AppointmentPolicy _policy = new();
    private readonly NotificationDeliveryOptions _delivery = new() { SmsEnabled = true, EmailEnabled = true };

    private int _polyclinicId;
    private int _otherPolyclinicId;
    private int _doctorId;
    private int _otherDoctorId;
    private int _patientId;
    private int _secondPatientId;

    public void Dispose() => _db.Dispose();

    private sealed record Services(AppDbContext Db, AvailabilityService Availability, AppointmentService Appointments, WaitlistService Waitlist, AppointmentMaintenance Maintenance);

    private Services Create()
    {
        var ctx = _db.CreateContext();
        var options = Options.Create(_policy);
        var availability = new AvailabilityService(ctx, _db.Clock, options);
        var notifications = new NotificationService(ctx, _db.Clock, Options.Create(_delivery));
        var appointments = new AppointmentService(ctx, _db.Clock, availability, notifications, new VisitService(ctx, _db.Clock), options);
        return new Services(ctx, availability, appointments, new WaitlistService(ctx, _db.Clock, options), new AppointmentMaintenance(ctx, _db.Clock, notifications, options, NullLogger<AppointmentMaintenance>.Instance));
    }

    private async Task SeedAsync()
    {
        var (polyclinic, _, _, patient) = await _db.SeedBasicsAsync();
        await using var ctx = _db.CreateContext();
        var other = new Polyclinic { Name = "Göz", FacilityId = polyclinic.FacilityId };
        ctx.Polyclinics.Add(other);
        var tracked = await ctx.Patients.SingleAsync(p => p.Id == patient.Id);
        tracked.Phone = "0555 000 00 01";
        tracked.Email = "ayse@example.com";
        var second = new Patient { FirstName = "Can", LastName = "Demir", Phone = "0555 000 00 02" };
        second.RefreshSearchName();
        ctx.Patients.Add(second);
        await ctx.SaveChangesAsync();

        var doctor = new Doctor { Title = "Uzm. Dr.", FirstName = "Ece", LastName = "Kaya", PolyclinicId = polyclinic.Id };
        var otherDoctor = new Doctor { FirstName = "Ali", LastName = "Göz", PolyclinicId = other.Id };
        ctx.Doctors.AddRange(doctor, otherDoctor);
        await ctx.SaveChangesAsync();

        foreach (var d in new[] { doctor, otherDoctor })
        {
            foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            {
                ctx.WorkSchedules.Add(new WorkSchedule(d.Id, day, new TimeOnly(9, 0), new TimeOnly(12, 0), 30, Today.AddDays(-30), null, true));
            }
        }

        await ctx.SaveChangesAsync();
        (_polyclinicId, _otherPolyclinicId, _doctorId, _otherDoctorId, _patientId, _secondPatientId) = (polyclinic.Id, other.Id, doctor.Id, otherDoctor.Id, patient.Id, second.Id);
    }

    private BookingRequest Request(DateOnly date, int hour, int minute = 0, int? patientId = null, int? doctorId = null)
        => new() { PatientId = patientId ?? _patientId, DoctorId = doctorId ?? _doctorId, Date = date, StartTime = new TimeOnly(hour, minute) };

    [Fact]
    public async Task Availability_RespectsCurrentTimeAndOnlineLeadTime()
    {
        await SeedAsync();
        var s = Create();

        var online = await s.Availability.GetFreeSlotsAsync(_polyclinicId, null, Today, Today, online: true);
        var desk = await s.Availability.GetFreeSlotsAsync(_polyclinicId, null, Today, Today, online: false);

        Assert.Equal([new TimeOnly(11, 0), new TimeOnly(11, 30)], online.Select(x => x.Start));
        Assert.Equal([new TimeOnly(10, 0), new TimeOnly(10, 30), new TimeOnly(11, 0), new TimeOnly(11, 30)], desk.Select(x => x.Start));
        Assert.All(online, x => Assert.Equal("Uzm. Dr. Ece Kaya", x.DoctorName));
    }

    [Fact]
    public async Task Availability_ExcludesLeaveHolidaysAndClosedOnlinePolyclinics()
    {
        await SeedAsync();
        await using (var ctx = _db.CreateContext())
        {
            ctx.DoctorLeaves.Add(new DoctorLeave(_doctorId, Tomorrow, Tomorrow, "Kongre"));
            ctx.Holidays.Add(new Holiday { Date = Tomorrow.AddDays(1), Name = "Tatil" });
            var eye = await ctx.Polyclinics.SingleAsync(p => p.Id == _otherPolyclinicId);
            eye.AcceptsOnlineAppointments = false;
            await ctx.SaveChangesAsync();
        }

        var s = Create();

        Assert.Empty(await s.Availability.GetFreeSlotsAsync(_polyclinicId, null, Tomorrow, Tomorrow.AddDays(1), online: false));
        Assert.Empty(await s.Availability.GetFreeSlotsAsync(_otherPolyclinicId, null, Tomorrow, Tomorrow, online: true));
        Assert.NotEmpty(await s.Availability.GetFreeSlotsAsync(_otherPolyclinicId, null, Tomorrow, Tomorrow, online: false));
    }

    [Fact]
    public async Task FindEarliest_SkipsFullyBookedDays()
    {
        await SeedAsync();
        await using (var ctx = _db.CreateContext())
        {
            ctx.DoctorLeaves.Add(new DoctorLeave(_doctorId, Today, Tomorrow, null));
            await ctx.SaveChangesAsync();
        }

        var earliest = await Create().Availability.FindEarliestAsync(_polyclinicId, null, online: true);

        Assert.NotNull(earliest);
        Assert.Equal(new DateOnly(2026, 11, 6), earliest.Date);
        Assert.Equal(new TimeOnly(9, 0), earliest.Start);
    }

    [Fact]
    public async Task Book_CreatesAppointmentAndQueuesConfirmation()
    {
        await SeedAsync();
        var s = Create();

        var appointment = await s.Appointments.BookAsync(Request(Tomorrow, 9, 30), AppointmentChannel.Online);

        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.Equal(new TimeOnly(10, 0), appointment.EndTime);
        var messages = await s.Db.Notifications.Where(n => n.Type == NotificationType.AppointmentBooked).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Channel == NotificationChannel.Sms && m.Body.Contains(appointment.ConfirmationCode, StringComparison.Ordinal));
        Assert.DoesNotContain(await s.Availability.GetFreeSlotsAsync(_polyclinicId, _doctorId, Tomorrow, Tomorrow, false), x => x.Start == new TimeOnly(9, 30));
    }

    [Fact]
    public async Task PortalPatients_ReceiveMessagesInThePortalEvenWithoutProviders()
    {
        await SeedAsync();
        await using (var ctx = _db.CreateContext())
        {
            var patient = await ctx.Patients.SingleAsync(p => p.Id == _patientId);
            patient.UserId = "portal-user";
            await ctx.SaveChangesAsync();
        }

        _delivery.SmsEnabled = false;
        _delivery.EmailEnabled = false;
        var s = Create();

        var appointment = await s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Online);

        var message = Assert.Single(await s.Db.Notifications.ToListAsync());
        Assert.Equal(NotificationChannel.Portal, message.Channel);
        Assert.Equal(NotificationStatus.Sent, message.Status);
        Assert.Contains(appointment.ConfirmationCode, message.Body, StringComparison.Ordinal);

        var inbox = new PatientInbox(s.Db, _db.Clock);
        Assert.Equal(1, await inbox.UnreadCountAsync(_patientId));
        await inbox.MarkAllReadAsync(_patientId);
        Assert.Equal(0, await inbox.UnreadCountAsync(_patientId));
        Assert.NotNull(Assert.Single(await inbox.ListAsync(_patientId)).ReadAtUtc);
    }

    [Fact]
    public async Task WithoutProvidersOrPortalAccount_NothingIsQueued()
    {
        await SeedAsync();
        _delivery.SmsEnabled = false;
        _delivery.EmailEnabled = false;
        var s = Create();

        await s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Desk);

        Assert.Empty(await s.Db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Book_RejectsSecondActiveAppointmentInTheSamePolyclinic()
    {
        await SeedAsync();
        var s = Create();
        await s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Online);

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.Appointments.BookAsync(Request(Tomorrow.AddDays(1), 9), AppointmentChannel.Online));

        Assert.Contains("zaten aktif bir randevunuz", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(await s.Appointments.BookAsync(Request(Tomorrow, 10, doctorId: _otherDoctorId), AppointmentChannel.Online));
    }

    [Fact]
    public async Task Book_RejectsATakenSlot()
    {
        await SeedAsync();
        var s = Create();
        await s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Online);

        await Assert.ThrowsAsync<DomainException>(() => s.Appointments.BookAsync(Request(Tomorrow, 9, patientId: _secondPatientId), AppointmentChannel.Desk));
    }

    [Fact]
    public async Task Database_PreventsDoubleBookingButAllowsReuseOfCancelledSlots()
    {
        await SeedAsync();
        await using var ctx = _db.CreateContext();
        var first = new Appointment(_patientId, _doctorId, _polyclinicId, Tomorrow, new TimeOnly(9, 0), new TimeOnly(9, 30), AppointmentChannel.Desk, null);
        ctx.Appointments.Add(first);
        await ctx.SaveChangesAsync();

        ctx.Appointments.Add(new Appointment(_secondPatientId, _doctorId, _polyclinicId, Tomorrow, new TimeOnly(9, 0), new TimeOnly(9, 30), AppointmentChannel.Desk, null));
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        ctx.ChangeTracker.Clear();

        var reloaded = await ctx.Appointments.SingleAsync(a => a.Id == first.Id);
        reloaded.Cancel(CancellationSource.Staff, null, _db.Clock.UtcNow);
        ctx.Appointments.Add(new Appointment(_secondPatientId, _doctorId, _polyclinicId, Tomorrow, new TimeOnly(9, 0), new TimeOnly(9, 30), AppointmentChannel.Desk, null));
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Book_OnlineHorizonIsShorterThanStaffHorizon()
    {
        await SeedAsync();
        var s = Create();
        var farAway = new DateOnly(2026, 12, 23); // a Wednesday, beyond 30 days

        await Assert.ThrowsAsync<DomainException>(() => s.Appointments.BookAsync(Request(farAway, 9), AppointmentChannel.Online));
        Assert.NotNull(await s.Appointments.BookAsync(Request(farAway, 9), AppointmentChannel.Desk));
    }

    [Fact]
    public async Task NoShow_RestrictsOnlineBookingInThatPolyclinicFor15Days()
    {
        await SeedAsync();
        await AddNoShowAsync(_polyclinicId, _doctorId, Today.AddDays(-3));
        var s = Create();

        var restriction = await s.Appointments.GetRestrictionAsync(_patientId, _polyclinicId);
        Assert.NotNull(restriction);
        Assert.False(restriction.AllPolyclinics);
        Assert.Equal(Today.AddDays(12), restriction.Until);

        await Assert.ThrowsAsync<DomainException>(() => s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Online));
        Assert.NotNull(await s.Appointments.BookAsync(Request(Tomorrow, 9, doctorId: _otherDoctorId), AppointmentChannel.Online));
        Assert.NotNull(await s.Appointments.BookAsync(Request(Tomorrow, 10), AppointmentChannel.Desk));
    }

    [Fact]
    public async Task RepeatedNoShows_RestrictAllPolyclinics()
    {
        await SeedAsync();
        await AddNoShowAsync(_polyclinicId, _doctorId, Today.AddDays(-5));
        await AddNoShowAsync(_polyclinicId, _doctorId, Today.AddDays(-2));
        var s = Create();

        var restriction = await s.Appointments.GetRestrictionAsync(_patientId, _otherPolyclinicId);

        Assert.NotNull(restriction);
        Assert.True(restriction.AllPolyclinics);
        await Assert.ThrowsAsync<DomainException>(() => s.Appointments.BookAsync(Request(Tomorrow, 9, doctorId: _otherDoctorId), AppointmentChannel.Online));
    }

    [Fact]
    public async Task OldNoShows_DoNotRestrict()
    {
        await SeedAsync();
        await AddNoShowAsync(_polyclinicId, _doctorId, Today.AddDays(-20));

        Assert.Null(await Create().Appointments.GetRestrictionAsync(_patientId, _polyclinicId));
    }

    [Fact]
    public async Task PatientCancellation_IsBlockedCloseToTheAppointment()
    {
        await SeedAsync();
        var s = Create();
        var soon = await s.Appointments.BookAsync(Request(Today, 11), AppointmentChannel.Desk);

        await Assert.ThrowsAsync<DomainException>(() => s.Appointments.CancelAsync(soon.Id, CancellationSource.Patient, null, _patientId));
        await s.Appointments.CancelAsync(soon.Id, CancellationSource.Staff, "Hekim izinli");

        var cancelled = await s.Appointments.GetAsync(soon.Id);
        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);
        Assert.True(await s.Db.Notifications.AnyAsync(n => n.Type == NotificationType.AppointmentCancelled && n.Body.Contains("Hekim izinli")));
    }

    [Fact]
    public async Task Patient_CannotCancelSomeoneElsesAppointment()
    {
        await SeedAsync();
        var s = Create();
        var appointment = await s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Online);

        await Assert.ThrowsAsync<Common.NotFoundException>(() => s.Appointments.CancelAsync(appointment.Id, CancellationSource.Patient, null, _secondPatientId));
    }

    [Fact]
    public async Task Cancellation_NotifiesTheWaitingList()
    {
        await SeedAsync();
        var s = Create();
        var appointment = await s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Online);
        var entryId = await s.Waitlist.JoinAsync(_secondPatientId, _polyclinicId, null, Today, Today.AddDays(7));

        await s.Appointments.CancelAsync(appointment.Id, CancellationSource.Patient, null, _patientId);

        var entry = await s.Db.WaitlistEntries.SingleAsync(w => w.Id == entryId);
        Assert.Equal(WaitlistStatus.Notified, entry.Status);
        Assert.True(await s.Db.Notifications.AnyAsync(n => n.PatientId == _secondPatientId && n.Type == NotificationType.WaitlistSlotAvailable));
    }

    [Fact]
    public async Task Booking_FulfilsThePatientsWaitingListEntry()
    {
        await SeedAsync();
        var s = Create();
        var entryId = await s.Waitlist.JoinAsync(_patientId, _polyclinicId, null, Today, Today.AddDays(7));

        await s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Online);

        Assert.Equal(WaitlistStatus.Fulfilled, (await s.Db.WaitlistEntries.SingleAsync(w => w.Id == entryId)).Status);
        Assert.Empty(await s.Waitlist.ListAsync(_patientId));
    }

    [Fact]
    public async Task Waitlist_RejectsDuplicateEntries()
    {
        await SeedAsync();
        var s = Create();
        await s.Waitlist.JoinAsync(_patientId, _polyclinicId, null, Today, Today.AddDays(7));

        await Assert.ThrowsAsync<DomainException>(() => s.Waitlist.JoinAsync(_patientId, _polyclinicId, _doctorId, Today, Today.AddDays(3)));
    }

    [Fact]
    public async Task Reschedule_MovesTheAppointmentAndReleasesTheOldSlot()
    {
        await SeedAsync();
        var s = Create();
        var original = await s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Online);

        var moved = await s.Appointments.RescheduleAsync(original.Id, Request(Tomorrow, 11), AppointmentChannel.Online, _patientId);

        Assert.Equal(original.Id, moved.RescheduledFromId);
        Assert.Equal(AppointmentStatus.Cancelled, (await s.Appointments.GetAsync(original.Id)).Status);
        Assert.Contains(await s.Availability.GetFreeSlotsAsync(_polyclinicId, _doctorId, Tomorrow, Tomorrow, true), x => x.Start == new TimeOnly(9, 0));
    }

    [Fact]
    public async Task CheckIn_OpensAVisitWithQueueNumber()
    {
        await SeedAsync();
        var s = Create();
        var appointment = await s.Appointments.BookAsync(Request(Today, 11), AppointmentChannel.Desk);

        var visitId = await s.Appointments.CheckInAsync(appointment.Id);

        var visit = await s.Db.Visits.Include(v => v.Items).SingleAsync(v => v.Id == visitId);
        Assert.Equal(_doctorId, visit.DoctorId);
        Assert.Equal(1, visit.QueueNumber);
        Assert.Single(visit.Items);
        var checkedIn = await s.Appointments.GetAsync(appointment.Id);
        Assert.Equal(AppointmentStatus.CheckedIn, checkedIn.Status);
        Assert.Equal(visitId, checkedIn.VisitId);
    }

    [Fact]
    public async Task CheckIn_OnlyOnTheAppointmentDay()
    {
        await SeedAsync();
        var s = Create();
        var appointment = await s.Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Desk);

        await Assert.ThrowsAsync<DomainException>(() => s.Appointments.CheckInAsync(appointment.Id));
    }

    [Fact]
    public async Task Maintenance_MarksNoShowsSendsRemindersAndExpiresWaitlist()
    {
        await SeedAsync();
        int pastId, futureId;
        await using (var ctx = _db.CreateContext())
        {
            var past = new Appointment(_patientId, _doctorId, _polyclinicId, Today.AddDays(-1), new TimeOnly(9, 0), new TimeOnly(9, 30), AppointmentChannel.Desk, null);
            var future = new Appointment(_secondPatientId, _doctorId, _polyclinicId, Tomorrow, new TimeOnly(9, 0), new TimeOnly(9, 30), AppointmentChannel.Online, null);
            ctx.Appointments.AddRange(past, future);
            ctx.WaitlistEntries.Add(new WaitlistEntry(_patientId, _otherPolyclinicId, null, Today.AddDays(-10), Today.AddDays(-1)));
            _db.Clock.UtcNow = _db.Clock.UtcNow.AddDays(-3); // booked three days ago
            await ctx.SaveChangesAsync();
            _db.Clock.UtcNow = _db.Clock.UtcNow.AddDays(3);
            (pastId, futureId) = (past.Id, future.Id);
        }

        var result = await Create().Maintenance.RunAsync();

        Assert.Equal(new MaintenanceResult(1, 1, 1), result);
        var s = Create();
        Assert.Equal(AppointmentStatus.NoShow, (await s.Appointments.GetAsync(pastId)).Status);
        Assert.NotNull((await s.Appointments.GetAsync(futureId)).ReminderSentAtUtc);
        Assert.True(await s.Db.Notifications.AnyAsync(n => n.PatientId == _secondPatientId && n.Type == NotificationType.AppointmentReminder));
        Assert.Equal(new MaintenanceResult(0, 0, 0), await s.Maintenance.RunAsync());
    }

    [Fact]
    public async Task Dispatcher_SendsPendingAndRetriesFailures()
    {
        await SeedAsync();
        await Create().Appointments.BookAsync(Request(Tomorrow, 9), AppointmentChannel.Online);
        await using var ctx = _db.CreateContext();
        var sms = new RecordingSender(NotificationChannel.Sms);
        var email = new RecordingSender(NotificationChannel.Email) { Fail = true };
        var dispatcher = new NotificationDispatcher(ctx, [sms, email], _db.Clock, NullLogger<NotificationDispatcher>.Instance);

        var sent = await dispatcher.DispatchPendingAsync();

        Assert.Equal(1, sent);
        Assert.Single(sms.Sent);
        var failed = await ctx.Notifications.SingleAsync(n => n.Channel == NotificationChannel.Email);
        Assert.Equal(NotificationStatus.Pending, failed.Status);
        Assert.Equal(1, failed.Attempts);
        Assert.Equal("SMTP down", failed.LastError);
    }

    private async Task AddNoShowAsync(int polyclinicId, int doctorId, DateOnly date)
    {
        await using var ctx = _db.CreateContext();
        var appointment = new Appointment(_patientId, doctorId, polyclinicId, date, new TimeOnly(9, 0), new TimeOnly(9, 30), AppointmentChannel.Online, null);
        appointment.MarkNoShow(date.ToDateTime(new TimeOnly(12, 0)));
        ctx.Appointments.Add(appointment);
        await ctx.SaveChangesAsync();
    }

    private sealed class RecordingSender(NotificationChannel channel) : INotificationSender
    {
        public NotificationChannel Channel { get; } = channel;

        public bool Fail { get; init; }

        public List<Notification> Sent { get; } = [];

        public Task SendAsync(Notification notification, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("SMTP down");
            }

            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }
}
