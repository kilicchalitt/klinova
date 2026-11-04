using Klinova.Domain.Common;

namespace Klinova.Domain.Settings;

/// <summary>Institution details printed on receipts and shown in the application header.</summary>
public class InstitutionProfile : AuditableEntity
{
    public const int SingletonId = 1;

    public InstitutionProfile()
    {
        Id = SingletonId;
    }

    public string Name { get; set; } = "Klinova Sağlık Merkezi";

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? TaxOffice { get; set; }

    public string? TaxNumber { get; set; }

    public string? ReceiptFooter { get; set; } = "Geçmiş olsun. Sağlıklı günler dileriz.";
}
