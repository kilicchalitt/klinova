using System.ComponentModel.DataAnnotations;
using Klinova.Domain.Common;
using Klinova.Domain.Visits;

namespace Klinova.Application.Visits;

public sealed class AdmissionInput
{
    /// <summary>Taken from the route, not from the form.</summary>
    public int PatientId { get; set; }

    [Display(Name = "Poliklinik")]
    [Range(1, int.MaxValue, ErrorMessage = "{0} seçiniz.")]
    public int PolyclinicId { get; set; }

    [Display(Name = "Hekim")]
    public int? DoctorId { get; set; }

    [Display(Name = "Şikâyet / Başvuru Nedeni")]
    [StringLength(FieldLengths.LongText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Complaint { get; set; }

    /// <summary>Services to add immediately, typically the examination fee.</summary>
    [Display(Name = "Hizmetler")]
    public List<int> ServiceIds { get; set; } = [];
}

public sealed class ExaminationInput
{
    [Display(Name = "Şikâyet")]
    [StringLength(FieldLengths.LongText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Complaint { get; set; }

    [Display(Name = "Tanı")]
    [StringLength(FieldLengths.LongText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Diagnosis { get; set; }

    [Display(Name = "Muayene Notları")]
    [StringLength(FieldLengths.LongText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Notes { get; set; }
}

/// <summary>Visit list filter. <c>DoctorId</c> also matches visits not yet assigned to a physician.</summary>
public sealed record VisitQuery(
    DateOnly? Date = null,
    int? PolyclinicId = null,
    int? DoctorId = null,
    VisitStatus? Status = null,
    int? PatientId = null);

public sealed record VisitListItem(
    int Id,
    DateOnly VisitDate,
    int QueueNumber,
    int PatientId,
    string PatientFileNumber,
    string PatientName,
    string? MedicalAlerts,
    int PolyclinicId,
    string PolyclinicName,
    int? DoctorId,
    string? DoctorName,
    VisitStatus Status,
    DateTime CheckInAtUtc,
    decimal TotalAmount,
    bool IsPaid);
