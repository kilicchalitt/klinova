using System.ComponentModel.DataAnnotations;

namespace Klinova.Domain.Patients;

public enum Gender
{
    [Display(Name = "Belirtilmemiş")]
    Unspecified = 0,

    [Display(Name = "Kadın")]
    Female = 1,

    [Display(Name = "Erkek")]
    Male = 2,
}

public enum BloodType
{
    [Display(Name = "Bilinmiyor")]
    Unknown = 0,

    [Display(Name = "A Rh+")]
    APositive = 1,

    [Display(Name = "A Rh-")]
    ANegative = 2,

    [Display(Name = "B Rh+")]
    BPositive = 3,

    [Display(Name = "B Rh-")]
    BNegative = 4,

    [Display(Name = "AB Rh+")]
    ABPositive = 5,

    [Display(Name = "AB Rh-")]
    ABNegative = 6,

    [Display(Name = "0 Rh+")]
    ZeroPositive = 7,

    [Display(Name = "0 Rh-")]
    ZeroNegative = 8,
}

public enum MaritalStatus
{
    [Display(Name = "Belirtilmemiş")]
    Unspecified = 0,

    [Display(Name = "Bekâr")]
    Unmarried = 1,

    [Display(Name = "Evli")]
    Married = 2,

    [Display(Name = "Boşanmış")]
    Divorced = 3,

    [Display(Name = "Dul")]
    Widowed = 4,
}

public enum InsuranceType
{
    [Display(Name = "Ücretli (Sigortasız)")]
    SelfPay = 0,

    [Display(Name = "SGK")]
    SocialSecurity = 1,

    [Display(Name = "Özel Sigorta")]
    PrivateInsurance = 2,

    [Display(Name = "Kurum Anlaşmalı")]
    Corporate = 3,
}
