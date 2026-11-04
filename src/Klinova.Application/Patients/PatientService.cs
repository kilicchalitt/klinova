using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Patients;

/// <summary>Patient registry: registration, updates, search and visit history.</summary>
public sealed class PatientService(IAppDbContext db)
{
    public async Task<PagedResult<PatientListItem>> SearchAsync(PatientSearchQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var patients = db.Patients.AsNoTracking();

        if (!query.IncludeArchived)
        {
            patients = patients.Where(p => !p.IsArchived);
        }

        var term = query.Term?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            if (term.Length == TurkishNationalId.Length && term.All(char.IsAsciiDigit))
            {
                patients = patients.Where(p => p.NationalId == term);
            }
            else if (Patient.TryParseFileNumber(term, out var fileId))
            {
                var digits = term;
                patients = patients.Where(p => p.Id == fileId || (p.Phone != null && p.Phone.Contains(digits)));
            }
            else
            {
                var normalized = SearchNormalizer.Normalize(term);
                patients = patients.Where(p => p.SearchName.Contains(normalized));
            }
        }

        var total = await patients.CountAsync(ct);
        var rows = await patients
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new { p.Id, p.FirstName, p.LastName, p.NationalId, p.BirthDate, p.Gender, p.Phone, p.InsuranceType, p.IsArchived })
            .ToListAsync(ct);

        var items = rows
            .Select(p => new PatientListItem(p.Id, Patient.FormatFileNumber(p.Id), $"{p.FirstName} {p.LastName}", p.NationalId, p.BirthDate, p.Gender, p.Phone, p.InsuranceType, p.IsArchived))
            .ToList();

        return new PagedResult<PatientListItem>(items, page, pageSize, total);
    }

    public async Task<Patient> GetAsync(int id, CancellationToken ct = default)
        => await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
           ?? throw NotFoundException.For("Hasta", id);

    public Task<Patient?> FindByNationalIdAsync(string nationalId, CancellationToken ct = default)
        => db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.NationalId == nationalId.Trim(), ct);

    public async Task<int> RegisterAsync(PatientInput input, CancellationToken ct = default)
    {
        await EnsureNationalIdIsUniqueAsync(input.NationalId, excludeId: null, ct);

        var patient = new Patient();
        input.ApplyTo(patient);
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);
        return patient.Id;
    }

    public async Task UpdateAsync(int id, PatientInput input, CancellationToken ct = default)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Hasta", id);

        await EnsureNationalIdIsUniqueAsync(input.NationalId, id, ct);
        input.ApplyTo(patient);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetArchivedAsync(int id, bool archived, CancellationToken ct = default)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Hasta", id);

        if (archived)
        {
            var hasOpenVisit = await db.Visits.AnyAsync(
                v => v.PatientId == id && (v.Status == Domain.Visits.VisitStatus.Waiting || v.Status == Domain.Visits.VisitStatus.InExamination),
                ct);
            if (hasOpenVisit)
            {
                throw new DomainException("Açık başvurusu bulunan hasta arşivlenemez.");
            }
        }

        patient.IsArchived = archived;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PatientVisitSummary>> GetVisitHistoryAsync(int patientId, CancellationToken ct = default)
    {
        var visits = await db.Visits.AsNoTracking()
            .Where(v => v.PatientId == patientId)
            .OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.Id)
            .Select(v => new
            {
                v.Id,
                v.VisitDate,
                v.QueueNumber,
                PolyclinicName = v.Polyclinic!.Name,
                DoctorTitle = v.Doctor != null ? v.Doctor.Title : null,
                DoctorFirst = v.Doctor != null ? v.Doctor.FirstName : null,
                DoctorLast = v.Doctor != null ? v.Doctor.LastName : null,
                v.Status,
                v.Diagnosis,
                Lines = v.Items.Select(i => new { i.Quantity, i.UnitPrice }).ToList(),
                IsPaid = v.Payment != null,
            })
            .ToListAsync(ct);

        return visits.Select(v => new PatientVisitSummary(
                v.Id,
                v.VisitDate,
                v.QueueNumber,
                v.PolyclinicName,
                v.DoctorFirst is null ? null : $"{v.DoctorTitle} {v.DoctorFirst} {v.DoctorLast}".Trim(),
                v.Status,
                v.Diagnosis,
                v.Lines.Sum(l => l.Quantity * l.UnitPrice),
                v.IsPaid))
            .ToList();
    }

    private async Task EnsureNationalIdIsUniqueAsync(string? nationalId, int? excludeId, CancellationToken ct)
    {
        var normalized = TurkishNationalId.Normalize(nationalId);
        if (normalized is null)
        {
            return;
        }

        var duplicate = await db.Patients.AsNoTracking()
            .Where(p => p.NationalId == normalized && (excludeId == null || p.Id != excludeId))
            .Select(p => new { p.Id, p.FirstName, p.LastName })
            .FirstOrDefaultAsync(ct);

        if (duplicate is not null)
        {
            throw new DomainException(
                $"Bu T.C. Kimlik No ile kayıtlı bir hasta zaten var: {duplicate.FirstName} {duplicate.LastName} (Dosya No {Patient.FormatFileNumber(duplicate.Id)}).");
        }
    }
}
