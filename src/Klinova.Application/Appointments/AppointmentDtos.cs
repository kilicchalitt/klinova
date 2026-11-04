using System.ComponentModel.DataAnnotations;
using Klinova.Domain.Appointments;

namespace Klinova.Application.Appointments;

public sealed class BookingRequest
{
    public int PatientId { get; set; }

    public int DoctorId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    [Display(Name = "Hekime not")]
    [StringLength(Appointment.NoteMaxLength, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Note { get; set; }
}

public sealed record AppointmentQuery(
    DateOnly? Date = null,
    int? FacilityId = null,
    int? PolyclinicId = null,
    int? DoctorId = null,
    AppointmentStatus? Status = null);

public sealed record AppointmentListItem(
    int Id,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int PatientId,
    string PatientName,
    string? PatientPhone,
    int DoctorId,
    string DoctorName,
    int PolyclinicId,
    string PolyclinicName,
    string FacilityName,
    AppointmentStatus Status,
    AppointmentChannel Channel,
    string ConfirmationCode,
    string? PatientNote,
    int? VisitId)
{
    public DateTime StartsAtLocal => Date.ToDateTime(StartTime);
}

/// <summary>Why a patient may currently not book online, if at all.</summary>
public sealed record BookingRestriction(DateOnly Until, bool AllPolyclinics);

public sealed record WaitlistListItem(
    int Id,
    int PatientId,
    string PatientName,
    string? PatientPhone,
    int PolyclinicId,
    string PolyclinicName,
    string FacilityName,
    string? DoctorName,
    DateOnly EarliestDate,
    DateOnly LatestDate,
    WaitlistStatus Status,
    DateTime CreatedAtUtc,
    DateTime? NotifiedAtUtc);
