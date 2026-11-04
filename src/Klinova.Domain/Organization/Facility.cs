using Klinova.Domain.Common;

namespace Klinova.Domain.Organization;

/// <summary>
/// A site of the institution: a hospital, campus building, branch clinic or
/// family health centre. Polyclinics belong to a facility, which lets one
/// installation serve several sites in different cities and districts.
/// </summary>
public class Facility : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Province ("il"), used to filter facilities in online booking.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>District ("ilçe").</summary>
    public string District { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Polyclinic> Polyclinics { get; } = [];
}
