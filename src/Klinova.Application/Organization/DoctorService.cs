using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Organization;

public sealed class DoctorInput
{
    [Display(Name = "Unvan")]
    [StringLength(FieldLengths.Title, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Title { get; set; }

    [Display(Name = "Ad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Soyad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Poliklinik")]
    [Range(1, int.MaxValue, ErrorMessage = "{0} seçiniz.")]
    public int PolyclinicId { get; set; }

    [Display(Name = "Diploma / Tescil No")]
    [StringLength(FieldLengths.Code * 2, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? RegistrationNumber { get; set; }

    [Display(Name = "Telefon")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    [StringLength(FieldLengths.Phone, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Phone { get; set; }

    [Display(Name = "E-posta")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [StringLength(FieldLengths.Email, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Email { get; set; }

    [Display(Name = "Kullanıcı Hesabı")]
    public string? UserId { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public sealed record DoctorListItem(int Id, string FullName, int PolyclinicId, string PolyclinicName, string? Phone, string? UserId, bool IsActive, string FacilityName = "");

/// <summary>Maintains physicians and their polyclinic assignments.</summary>
public sealed class DoctorService(IAppDbContext db)
{
    public async Task<List<DoctorListItem>> ListAsync(int? polyclinicId = null, CancellationToken ct = default)
    {
        var rows = await db.Doctors.AsNoTracking()
            .Where(d => polyclinicId == null || d.PolyclinicId == polyclinicId)
            .Select(d => new { d.Id, d.Title, d.FirstName, d.LastName, d.PolyclinicId, PolyclinicName = d.Polyclinic!.Name, FacilityName = d.Polyclinic.Facility!.Name, d.Phone, d.UserId, d.IsActive })
            .ToListAsync(ct);

        return rows
            .OrderBy(d => d.FacilityName, TurkishCollation.Comparer)
            .ThenBy(d => d.PolyclinicName, TurkishCollation.Comparer)
            .ThenBy(d => d.FirstName, TurkishCollation.Comparer)
            .ThenBy(d => d.LastName, TurkishCollation.Comparer)
            .Select(d => new DoctorListItem(d.Id, FullName(d.Title, d.FirstName, d.LastName), d.PolyclinicId, d.PolyclinicName, d.Phone, d.UserId, d.IsActive, d.FacilityName))
            .ToList();
    }

    public async Task<List<LookupItem>> LookupAsync(int? polyclinicId = null, CancellationToken ct = default)
    {
        var rows = await db.Doctors.AsNoTracking()
            .Where(d => d.IsActive && (polyclinicId == null || d.PolyclinicId == polyclinicId))
            .Select(d => new { d.Id, d.Title, d.FirstName, d.LastName })
            .ToListAsync(ct);
        return rows
            .OrderBy(d => d.FirstName, TurkishCollation.Comparer)
            .ThenBy(d => d.LastName, TurkishCollation.Comparer)
            .Select(d => new LookupItem(d.Id, FullName(d.Title, d.FirstName, d.LastName)))
            .ToList();
    }

    /// <summary>Returns the active physician record linked to a user account, if any.</summary>
    public Task<Doctor?> FindByUserIdAsync(string? userId, CancellationToken ct = default)
        => userId is null
            ? Task.FromResult<Doctor?>(null)
            : db.Doctors.AsNoTracking().Include(d => d.Polyclinic).FirstOrDefaultAsync(d => d.UserId == userId && d.IsActive, ct);

    public async Task<DoctorInput> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var d = await db.Doctors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw NotFoundException.For("Hekim", id);
        return new DoctorInput
        {
            Title = d.Title,
            FirstName = d.FirstName,
            LastName = d.LastName,
            PolyclinicId = d.PolyclinicId,
            RegistrationNumber = d.RegistrationNumber,
            Phone = d.Phone,
            Email = d.Email,
            UserId = d.UserId,
            IsActive = d.IsActive,
        };
    }

    public async Task<int> CreateAsync(DoctorInput input, CancellationToken ct = default)
    {
        await ValidateAsync(input, null, ct);
        var doctor = new Doctor();
        Apply(input, doctor);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync(ct);
        return doctor.Id;
    }

    public async Task UpdateAsync(int id, DoctorInput input, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw NotFoundException.For("Hekim", id);
        await ValidateAsync(input, id, ct);
        Apply(input, doctor);
        await db.SaveChangesAsync(ct);
    }

    internal static string FullName(string? title, string first, string last)
        => string.IsNullOrWhiteSpace(title) ? $"{first} {last}" : $"{title} {first} {last}";

    private static void Apply(DoctorInput input, Doctor d)
    {
        d.Title = Text.Clean(input.Title);
        d.FirstName = Text.Required(input.FirstName);
        d.LastName = Text.Required(input.LastName);
        d.PolyclinicId = input.PolyclinicId;
        d.RegistrationNumber = Text.Clean(input.RegistrationNumber);
        d.Phone = Text.Clean(input.Phone);
        d.Email = Text.Clean(input.Email);
        d.UserId = Text.Clean(input.UserId);
        d.IsActive = input.IsActive;
    }

    private async Task ValidateAsync(DoctorInput input, int? excludeId, CancellationToken ct)
    {
        if (!await db.Polyclinics.AnyAsync(p => p.Id == input.PolyclinicId, ct))
        {
            throw new DomainException("Seçilen poliklinik bulunamadı.");
        }

        var userId = Text.Clean(input.UserId);
        if (userId is not null && await db.Doctors.AnyAsync(d => d.UserId == userId && (excludeId == null || d.Id != excludeId), ct))
        {
            throw new DomainException("Seçilen kullanıcı hesabı başka bir hekime bağlı.");
        }
    }
}
