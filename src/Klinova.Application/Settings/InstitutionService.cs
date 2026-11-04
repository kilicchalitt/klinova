using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Settings;

public sealed class InstitutionInput
{
    [Display(Name = "Kurum Adı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.ShortText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Adres")]
    [StringLength(FieldLengths.Address, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Address { get; set; }

    [Display(Name = "Telefon")]
    [StringLength(FieldLengths.Phone, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Phone { get; set; }

    [Display(Name = "E-posta")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [StringLength(FieldLengths.Email, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Email { get; set; }

    [Display(Name = "Vergi Dairesi")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? TaxOffice { get; set; }

    [Display(Name = "Vergi No")]
    [StringLength(FieldLengths.Code, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? TaxNumber { get; set; }

    [Display(Name = "Makbuz Alt Notu")]
    [StringLength(FieldLengths.Address, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? ReceiptFooter { get; set; }
}

/// <summary>Reads and updates the single institution profile record.</summary>
public sealed class InstitutionService(IAppDbContext db)
{
    public async Task<InstitutionProfile> GetAsync(CancellationToken ct = default)
        => await db.InstitutionProfiles.AsNoTracking().FirstOrDefaultAsync(ct) ?? new InstitutionProfile();

    public async Task<InstitutionInput> GetForEditAsync(CancellationToken ct = default)
    {
        var p = await GetAsync(ct);
        return new InstitutionInput
        {
            Name = p.Name,
            Address = p.Address,
            Phone = p.Phone,
            Email = p.Email,
            TaxOffice = p.TaxOffice,
            TaxNumber = p.TaxNumber,
            ReceiptFooter = p.ReceiptFooter,
        };
    }

    public async Task UpdateAsync(InstitutionInput input, CancellationToken ct = default)
    {
        var profile = await db.InstitutionProfiles.FirstOrDefaultAsync(ct);
        if (profile is null)
        {
            profile = new InstitutionProfile();
            db.InstitutionProfiles.Add(profile);
        }

        profile.Name = Text.Required(input.Name);
        profile.Address = Text.Clean(input.Address);
        profile.Phone = Text.Clean(input.Phone);
        profile.Email = Text.Clean(input.Email);
        profile.TaxOffice = Text.Clean(input.TaxOffice);
        profile.TaxNumber = Text.Clean(input.TaxNumber);
        profile.ReceiptFooter = Text.Clean(input.ReceiptFooter);
        await db.SaveChangesAsync(ct);
    }
}
