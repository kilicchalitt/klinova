using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Organization;

public sealed class FacilityInput
{
    [Display(Name = "Kurum / Şube Adı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.ShortText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "İl")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string City { get; set; } = string.Empty;

    [Display(Name = "İlçe")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string District { get; set; } = string.Empty;

    [Display(Name = "Adres")]
    [StringLength(FieldLengths.Address, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Address { get; set; }

    [Display(Name = "Telefon")]
    [StringLength(FieldLengths.Phone, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Phone { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public sealed record FacilityListItem(int Id, string Name, string City, string District, string? Address, string? Phone, bool IsActive, int PolyclinicCount);

/// <summary>Sites of the institution (hospitals, campuses, branch clinics).</summary>
public sealed class FacilityService(IAppDbContext db)
{
    public async Task<List<FacilityListItem>> ListAsync(CancellationToken ct = default)
        => (await db.Facilities.AsNoTracking()
                .Select(f => new FacilityListItem(f.Id, f.Name, f.City, f.District, f.Address, f.Phone, f.IsActive, f.Polyclinics.Count(p => p.IsActive)))
                .ToListAsync(ct))
            .OrderBy(f => f.City, TurkishCollation.Comparer).ThenBy(f => f.Name, TurkishCollation.Comparer).ToList();

    public async Task<List<LookupItem>> LookupAsync(bool activeOnly = true, string? city = null, CancellationToken ct = default)
        => (await db.Facilities.AsNoTracking()
                .Where(f => (!activeOnly || f.IsActive) && (city == null || f.City == city))
                .Select(f => new LookupItem(f.Id, f.Name))
                .ToListAsync(ct))
            .SortBy(f => f.Name);

    /// <summary>Provinces that have at least one active facility, for the online booking wizard.</summary>
    public async Task<List<string>> CitiesAsync(CancellationToken ct = default)
        => (await db.Facilities.AsNoTracking().Where(f => f.IsActive).Select(f => f.City).Distinct().ToListAsync(ct))
            .SortBy(c => c);

    public async Task<Facility> GetAsync(int id, CancellationToken ct = default)
        => await db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct)
           ?? throw NotFoundException.For("Kurum", id);

    public async Task<FacilityInput> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var f = await GetAsync(id, ct);
        return new FacilityInput { Name = f.Name, City = f.City, District = f.District, Address = f.Address, Phone = f.Phone, IsActive = f.IsActive };
    }

    public async Task<int> CreateAsync(FacilityInput input, CancellationToken ct = default)
    {
        await EnsureUniqueAsync(input.Name, null, ct);
        var facility = new Facility();
        Apply(input, facility);
        db.Facilities.Add(facility);
        await db.SaveChangesAsync(ct);
        return facility.Id;
    }

    public async Task UpdateAsync(int id, FacilityInput input, CancellationToken ct = default)
    {
        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw NotFoundException.For("Kurum", id);
        await EnsureUniqueAsync(input.Name, id, ct);
        Apply(input, facility);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(FacilityInput input, Facility f)
    {
        f.Name = Text.Required(input.Name);
        f.City = Text.Required(input.City);
        f.District = Text.Required(input.District);
        f.Address = Text.Clean(input.Address);
        f.Phone = Text.Clean(input.Phone);
        f.IsActive = input.IsActive;
    }

    private async Task EnsureUniqueAsync(string name, int? excludeId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await db.Facilities.AnyAsync(f => f.Name == trimmed && (excludeId == null || f.Id != excludeId), ct))
        {
            throw new DomainException($"'{trimmed}' adında bir kurum zaten var.");
        }
    }
}
