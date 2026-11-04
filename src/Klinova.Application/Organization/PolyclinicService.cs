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

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public sealed record PolyclinicListItem(int Id, string Name, string? Location, string? Description, bool IsActive, int DoctorCount);

/// <summary>Maintains the institution's outpatient departments.</summary>
public sealed class PolyclinicService(IAppDbContext db)
{
    public Task<List<PolyclinicListItem>> ListAsync(CancellationToken ct = default)
        => db.Polyclinics.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PolyclinicListItem(p.Id, p.Name, p.Location, p.Description, p.IsActive, p.Doctors.Count(d => d.IsActive)))
            .ToListAsync(ct);

    public Task<List<LookupItem>> LookupAsync(bool activeOnly = true, CancellationToken ct = default)
        => db.Polyclinics.AsNoTracking()
            .Where(p => !activeOnly || p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new LookupItem(p.Id, p.Name))
            .ToListAsync(ct);

    public async Task<PolyclinicInput> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Polyclinics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw NotFoundException.For("Poliklinik", id);
        return new PolyclinicInput { Name = p.Name, Location = p.Location, Description = p.Description, IsActive = p.IsActive };
    }

    public async Task<int> CreateAsync(PolyclinicInput input, CancellationToken ct = default)
    {
        await EnsureUniqueNameAsync(input.Name, null, ct);
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
        await EnsureUniqueNameAsync(input.Name, id, ct);
        Apply(input, polyclinic);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(PolyclinicInput input, Polyclinic p)
    {
        p.Name = Text.Required(input.Name);
        p.Location = Text.Clean(input.Location);
        p.Description = Text.Clean(input.Description);
        p.IsActive = input.IsActive;
    }

    private async Task EnsureUniqueNameAsync(string name, int? excludeId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        var exists = await db.Polyclinics.AnyAsync(p => p.Name == trimmed && (excludeId == null || p.Id != excludeId), ct);
        if (exists)
        {
            throw new DomainException($"'{trimmed}' adında bir poliklinik zaten var.");
        }
    }
}
