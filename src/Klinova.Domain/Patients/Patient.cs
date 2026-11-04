using Klinova.Domain.Common;

namespace Klinova.Domain.Patients;

/// <summary>A registered patient. The file number shown to users is derived from the identifier.</summary>
public class Patient : AuditableEntity
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>T.C. Kimlik No or foreign identity number. Optional for newborns and tourists.</summary>
    public string? NationalId { get; set; }

    public string? PassportNumber { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? BirthPlace { get; set; }

    public Gender Gender { get; set; }

    public BloodType BloodType { get; set; }

    public MaritalStatus MaritalStatus { get; set; }

    public string? MotherName { get; set; }

    public string? FatherName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public InsuranceType InsuranceType { get; set; }

    public string? InsuranceInstitution { get; set; }

    public string? InsuranceNumber { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    /// <summary>Known allergies and chronic conditions shown prominently to physicians.</summary>
    public string? MedicalAlerts { get; set; }

    public string? Notes { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>Normalised "first last" name used for accent-insensitive searching.</summary>
    public string SearchName { get; private set; } = string.Empty;

    /// <summary>Human friendly, zero padded file number ("Dosya No").</summary>
    public string FileNumber => FormatFileNumber(Id);

    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Must be called after the name changes so that searching keeps working.</summary>
    public void RefreshSearchName() => SearchName = SearchNormalizer.Normalize(FullName);

    public static string FormatFileNumber(int id) => id.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Parses a file number typed by a user ("00000042" or "42").</summary>
    public static bool TryParseFileNumber(string? value, out int id)
    {
        id = 0;
        return !string.IsNullOrWhiteSpace(value)
            && value.Trim().Length <= 10
            && int.TryParse(value.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out id)
            && id > 0;
    }

    public int? AgeOn(DateOnly date)
    {
        if (BirthDate is not { } birth)
        {
            return null;
        }

        var age = date.Year - birth.Year;
        if (birth > date.AddYears(-age))
        {
            age--;
        }

        return Math.Max(age, 0);
    }
}
