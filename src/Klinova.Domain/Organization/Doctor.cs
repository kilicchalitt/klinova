using Klinova.Domain.Common;

namespace Klinova.Domain.Organization;

/// <summary>A physician working in a polyclinic, optionally linked to a login account.</summary>
public class Doctor : AuditableEntity
{
    /// <summary>Academic or professional title, e.g. "Uzm. Dr.".</summary>
    public string? Title { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public int PolyclinicId { get; set; }

    public Polyclinic? Polyclinic { get; set; }

    /// <summary>Diploma or Ministry of Health registration number.</summary>
    public string? RegistrationNumber { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>Identifier of the user account the physician signs in with, if any.</summary>
    public string? UserId { get; set; }

    public bool IsActive { get; set; } = true;

    public string FullName => string.IsNullOrWhiteSpace(Title)
        ? $"{FirstName} {LastName}"
        : $"{Title} {FirstName} {LastName}";
}
