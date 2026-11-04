using Klinova.Domain.Common;

namespace Klinova.Domain.Services;

/// <summary>An entry of the institution's price list (examination, laboratory test, imaging…).</summary>
public class MedicalService : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Category { get; set; }

    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; } = true;
}
