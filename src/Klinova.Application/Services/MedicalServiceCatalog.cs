using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Services;

public sealed class MedicalServiceInput
{
    [Display(Name = "Hizmet Kodu")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.Code, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Hizmet Adı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.ShortText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Kategori")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Category { get; set; }

    [Display(Name = "Birim Fiyat (₺)")]
    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "{0} 0 ile 1.000.000 arasında olmalıdır.")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public sealed record MedicalServiceListItem(int Id, string Code, string Name, string? Category, decimal UnitPrice, bool IsActive);

/// <summary>Maintains the price list of billable medical services.</summary>
public sealed class MedicalServiceCatalog(IAppDbContext db)
{
    public async Task<List<MedicalServiceListItem>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
        => (await db.MedicalServices.AsNoTracking()
                .Where(s => !activeOnly || s.IsActive)
                .Select(s => new MedicalServiceListItem(s.Id, s.Code, s.Name, s.Category, s.UnitPrice, s.IsActive))
                .ToListAsync(ct))
            .OrderBy(s => s.Category ?? string.Empty, TurkishCollation.Comparer)
            .ThenBy(s => s.Name, TurkishCollation.Comparer)
            .ToList();

    public async Task<MedicalServiceInput> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var s = await db.MedicalServices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw NotFoundException.For("Hizmet", id);
        return new MedicalServiceInput { Code = s.Code, Name = s.Name, Category = s.Category, UnitPrice = s.UnitPrice, IsActive = s.IsActive };
    }

    public async Task<int> CreateAsync(MedicalServiceInput input, CancellationToken ct = default)
    {
        await EnsureUniqueCodeAsync(input.Code, null, ct);
        var service = new MedicalService();
        Apply(input, service);
        db.MedicalServices.Add(service);
        await db.SaveChangesAsync(ct);
        return service.Id;
    }

    public async Task UpdateAsync(int id, MedicalServiceInput input, CancellationToken ct = default)
    {
        var service = await db.MedicalServices.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw NotFoundException.For("Hizmet", id);
        await EnsureUniqueCodeAsync(input.Code, id, ct);
        Apply(input, service);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(MedicalServiceInput input, MedicalService s)
    {
        s.Code = Text.Required(input.Code).ToUpperInvariant();
        s.Name = Text.Required(input.Name);
        s.Category = Text.Clean(input.Category);
        s.UnitPrice = decimal.Round(input.UnitPrice, 2, MidpointRounding.AwayFromZero);
        s.IsActive = input.IsActive;
    }

    private async Task EnsureUniqueCodeAsync(string code, int? excludeId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.MedicalServices.AnyAsync(s => s.Code == normalized && (excludeId == null || s.Id != excludeId), ct))
        {
            throw new DomainException($"'{normalized}' kodlu bir hizmet zaten var.");
        }
    }
}
