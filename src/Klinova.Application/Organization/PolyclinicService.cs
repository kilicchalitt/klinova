using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Organization;

public sealed class PolyclinicInput
{
    [Display(Name = "Poliklinik Adı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.ShortText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Konum (kat / oda)")]
    [StringLength(FieldLengths.ShortText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Location { get; set; }

    [Display(Name = "Açıklama")]
    [StringLength(FieldLengths.Address, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Description { get; set; }

    [Display(Name = "Kurum / Şube")]
    [Range(1, int.MaxValue, ErrorMessage = "{0} seçiniz.")]
    public int FacilityId { get; set; }

    [Display(Name = "Online randevu verilir")]
    public bool AcceptsOnlineAppointments { get; set; } = true;

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public sealed record PolyclinicListItem(int Id, string Name, string? Location, string? Description, bool IsActive, int DoctorCount, int FacilityId, string FacilityName, bool AcceptsOnlineAppointments);

/// <summary>Maintains the institution's outpatient departments.</summary>
public sealed class PolyclinicService(IAppDbContext db)
{
    public async Task<List<PolyclinicListItem>> ListAsync(CancellationToken ct = default)
        => (await db.Polyclinics.AsNoTracking()
                .Select(p => new PolyclinicListItem(p.Id, p.Name, p.Location, p.Description, p.IsActive, p.Doctors.Count(d => d.IsActive), p.FacilityId, p.Facility!.Name, p.AcceptsOnlineAppointments))
                .ToListAsync(ct))
            .OrderBy(p => p.FacilityName, TurkishCollation.Comparer)
            .ThenBy(p => p.Name, TurkishCollation.Comparer)
            .ToList();

    /// <summary>
    /// Drop-down items. When the institution has several facilities the facility
    /// name is appended so that departments with the same name can be told apart.
    /// </summary>
    public async Task<List<LookupItem>> LookupAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var rows = await db.Polyclinics.AsNoTracking()
            .Where(p => !activeOnly || (p.IsActive && p.Facility!.IsActive))
            .Select(p => new { p.Id, p.Name, Facility = p.Facility!.Name, p.FacilityId })
            .ToListAsync(ct);
        var multipleFacilities = rows.Select(r => r.FacilityId).Distinct().Count() > 1;
        return rows
            .Select(r => new LookupItem(r.Id, multipleFacilities ? $"{r.Name} — {r.Facility}" : r.Name))
            .SortBy(r => r.Name);
    }

    /// <summary>Active polyclinics of a facility that accept online appointments.</summary>
    public async Task<List<LookupItem>> OnlineLookupAsync(int facilityId, CancellationToken ct = default)
        => (await db.Polyclinics.AsNoTracking()
                .Where(p => p.FacilityId == facilityId && p.IsActive && p.AcceptsOnlineAppointments)
                .Select(p => new LookupItem(p.Id, p.Name))
                .ToListAsync(ct))
            .SortBy(p => p.Name);

    public async Task<PolyclinicInput> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Polyclinics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw NotFoundException.For("Poliklinik", id);
        return new PolyclinicInput { Name = p.Name, Location = p.Location, Description = p.Description, IsActive = p.IsActive, FacilityId = p.FacilityId, AcceptsOnlineAppointments = p.AcceptsOnlineAppointments };
    }

    public async Task<int> CreateAsync(PolyclinicInput input, CancellationToken ct = default)
    {
        await EnsureUniqueNameAsync(input, null, ct);
        var polyclinic = new Polyclinic();
        Apply(input, polyclinic);
        db.Polyclinics.Add(polyclinic);
        await db.SaveChangesAsync(ct);
        return polyclinic.Id;
    }

    public async Task UpdateAsync(int id, PolyclinicInput input, CancellationToken ct = default)
    {
        var polyclinic = await db.Polyclinics.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw NotFoundException.For("Poliklinik", id);
        await EnsureUniqueNameAsync(input, id, ct);
        Apply(input, polyclinic);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(PolyclinicInput input, Polyclinic p)
    {
        p.Name = Text.Required(input.Name);
        p.Location = Text.Clean(input.Location);
        p.Description = Text.Clean(input.Description);
        p.IsActive = input.IsActive;
        p.FacilityId = input.FacilityId;
        p.AcceptsOnlineAppointments = input.AcceptsOnlineAppointments;
    }

    private async Task EnsureUniqueNameAsync(PolyclinicInput input, int? excludeId, CancellationToken ct)
    {
        if (!await db.Facilities.AnyAsync(f => f.Id == input.FacilityId, ct))
        {
            throw new DomainException("Seçilen kurum bulunamadı.");
        }

        var trimmed = input.Name.Trim();
        var exists = await db.Polyclinics.AnyAsync(p => p.FacilityId == input.FacilityId && p.Name == trimmed && (excludeId == null || p.Id != excludeId), ct);
        if (exists)
        {
            throw new DomainException($"Bu kurumda '{trimmed}' adında bir poliklinik zaten var.");
        }
    }
}
