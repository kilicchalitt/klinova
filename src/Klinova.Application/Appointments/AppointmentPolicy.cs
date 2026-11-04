namespace Klinova.Application.Appointments;

/// <summary>
/// Booking rules of the institution, bound from the <c>Klinova:Appointments</c>
/// configuration section. The defaults follow the practice of Türkiye's central
/// appointment system (MHRS) and can be tightened or relaxed per installation.
/// </summary>
public sealed class AppointmentPolicy
{
    public const string SectionName = "Klinova:Appointments";

    /// <summary>How many days ahead patients can book online.</summary>
    public int BookingHorizonDays { get; set; } = 30;

    /// <summary>How many days ahead staff can book (phone / desk).</summary>
    public int StaffBookingHorizonDays { get; set; } = 90;

    /// <summary>Online bookings must start at least this many minutes from now.</summary>
    public int MinimumLeadMinutes { get; set; } = 60;

    /// <summary>Maximum number of upcoming online appointments per patient.</summary>
    public int MaxActivePerPatient { get; set; } = 5;

    /// <summary>Maximum number of upcoming appointments per patient in the same polyclinic (MHRS: 1).</summary>
    public int MaxActivePerPolyclinic { get; set; } = 1;

    /// <summary>Patients can cancel online until this many hours before the appointment.</summary>
    public int CancellationDeadlineHours { get; set; } = 2;

    /// <summary>After a no-show the patient cannot book the same polyclinic online for this many days (MHRS: 15).</summary>
    public int NoShowRestrictionDays { get; set; } = 15;

    /// <summary>Number of no-shows within the restriction period after which all polyclinics are restricted.</summary>
    public int NoShowsForGlobalRestriction { get; set; } = 2;

    /// <summary>Reminders are sent this many hours before the appointment.</summary>
    public int ReminderHoursBefore { get; set; } = 24;

    /// <summary>Maximum number of open waiting-list requests per patient.</summary>
    public int MaxWaitlistEntriesPerPatient { get; set; } = 3;

    /// <summary>How many waiting patients are notified when a slot becomes free.</summary>
    public int WaitlistNotifyCount { get; set; } = 3;
}
