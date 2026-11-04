using System.ComponentModel.DataAnnotations;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Domain.Patients;

namespace Klinova.Application.Patients;

/// <summary>Form model for registering or updating a patient.</summary>
public sealed class PatientInput
{
    [Display(Name = "Ad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Soyad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "T.C. Kimlik No")]
    [TurkishNationalId]
    public string? NationalId { get; set; }

    [Display(Name = "Pasaport No")]
    [StringLength(FieldLengths.Code, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? PassportNumber { get; set; }

    [Display(Name = "Doğum Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? BirthDate { get; set; }

    [Display(Name = "Doğum Yeri")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? BirthPlace { get; set; }

    [Display(Name = "Cinsiyet")]
    public Gender Gender { get; set; }

    [Display(Name = "Kan Grubu")]
    public BloodType BloodType { get; set; }

    [Display(Name = "Medeni Hâl")]
    public MaritalStatus MaritalStatus { get; set; }

    [Display(Name = "Anne Adı")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? MotherName { get; set; }

    [Display(Name = "Baba Adı")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? FatherName { get; set; }

    [Display(Name = "Telefon")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    [StringLength(FieldLengths.Phone, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Phone { get; set; }

    [Display(Name = "E-posta")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [StringLength(FieldLengths.Email, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Email { get; set; }

    [Display(Name = "Adres")]
    [StringLength(FieldLengths.Address, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Address { get; set; }

    [Display(Name = "Güvence Türü")]
    public InsuranceType InsuranceType { get; set; }

    [Display(Name = "Kurum Adı")]
    [StringLength(FieldLengths.ShortText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? InsuranceInstitution { get; set; }

    [Display(Name = "Kurum Sicil / Poliçe No")]
    [StringLength(FieldLengths.Code * 2, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? InsuranceNumber { get; set; }

    [Display(Name = "Yakınının Adı Soyadı")]
    [StringLength(FieldLengths.ShortText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? EmergencyContactName { get; set; }

    [Display(Name = "Yakınının Telefonu")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    [StringLength(FieldLengths.Phone, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? EmergencyContactPhone { get; set; }

    [Display(Name = "Tıbbi Uyarılar (alerji, kronik hastalık)")]
    [StringLength(FieldLengths.LongText / 4, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? MedicalAlerts { get; set; }

    [Display(Name = "Notlar")]
    [StringLength(FieldLengths.LongText, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Notes { get; set; }

    public static PatientInput FromEntity(Patient p) => new()
    {
        FirstName = p.FirstName,
        LastName = p.LastName,
        NationalId = p.NationalId,
        PassportNumber = p.PassportNumber,
        BirthDate = p.BirthDate,
        BirthPlace = p.BirthPlace,
        Gender = p.Gender,
        BloodType = p.BloodType,
        MaritalStatus = p.MaritalStatus,
        MotherName = p.MotherName,
        FatherName = p.FatherName,
        Phone = p.Phone,
        Email = p.Email,
        Address = p.Address,
        InsuranceType = p.InsuranceType,
        InsuranceInstitution = p.InsuranceInstitution,
        InsuranceNumber = p.InsuranceNumber,
        EmergencyContactName = p.EmergencyContactName,
        EmergencyContactPhone = p.EmergencyContactPhone,
        MedicalAlerts = p.MedicalAlerts,
        Notes = p.Notes,
    };

    internal void ApplyTo(Patient p)
    {
        p.FirstName = Text.Required(FirstName);
        p.LastName = Text.Required(LastName);
        p.NationalId = TurkishNationalId.Normalize(NationalId);
        p.PassportNumber = Text.Clean(PassportNumber);
        p.BirthDate = BirthDate;
        p.BirthPlace = Text.Clean(BirthPlace);
        p.Gender = Gender;
        p.BloodType = BloodType;
        p.MaritalStatus = MaritalStatus;
        p.MotherName = Text.Clean(MotherName);
        p.FatherName = Text.Clean(FatherName);
        p.Phone = Text.Clean(Phone);
        p.Email = Text.Clean(Email);
        p.Address = Text.Clean(Address);
        p.InsuranceType = InsuranceType;
        p.InsuranceInstitution = Text.Clean(InsuranceInstitution);
        p.InsuranceNumber = Text.Clean(InsuranceNumber);
        p.EmergencyContactName = Text.Clean(EmergencyContactName);
        p.EmergencyContactPhone = Text.Clean(EmergencyContactPhone);
        p.MedicalAlerts = Text.Clean(MedicalAlerts);
        p.Notes = Text.Clean(Notes);
        p.RefreshSearchName();
    }
}
