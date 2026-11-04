using Klinova.Domain.Patients;
using Klinova.Domain.Visits;

namespace Klinova.Application.Patients;

public sealed record PatientListItem(
    int Id,
    string FileNumber,
    string FullName,
    string? NationalId,
    DateOnly? BirthDate,
    Gender Gender,
    string? Phone,
    InsuranceType InsuranceType,
    bool IsArchived);

public sealed record PatientVisitSummary(
    int VisitId,
    DateOnly VisitDate,
    int QueueNumber,
    string PolyclinicName,
    string? DoctorName,
    VisitStatus Status,
    string? Diagnosis,
    decimal TotalAmount,
    bool IsPaid);

public sealed record PatientSearchQuery(string? Term = null, bool IncludeArchived = false, int Page = 1, int PageSize = 20);
