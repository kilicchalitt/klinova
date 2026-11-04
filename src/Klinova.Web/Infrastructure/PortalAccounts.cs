using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Domain.Patients;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Web.Infrastructure;

public sealed class PortalRegistrationInput
{
    [Display(Name = "T.C. Kimlik No")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [TurkishNationalId]
    public string NationalId { get; set; } = string.Empty;

    [Display(Name = "Ad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Soyad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(FieldLengths.Name, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Doğum Tarihi")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    public DateOnly? BirthDate { get; set; }

    [Display(Name = "Cep Telefonu")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    [StringLength(FieldLengths.Phone, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "E-posta")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [StringLength(FieldLengths.Email, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Email { get; set; }

    [Display(Name = "Şifre")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "{0} en az {2} karakter olmalıdır.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Şifre (Tekrar)")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Aydınlatma metnini okudum, kabul ediyorum")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "Devam etmek için aydınlatma metnini kabul etmelisiniz.")]
    public bool AcceptPrivacyNotice { get; set; }
}

/// <summary>
/// Self-service patient accounts. A patient already registered at the institution is
/// linked only when birth date, name and the phone number on file all match, so that
/// knowing someone's (non-secret) national id is not enough to read their records.
/// </summary>
public sealed class PortalAccounts(IAppDbContext db, UserManager<AppUser> users, IClock clock)
{
    private const string MismatchMessage =
        "Girdiğiniz bilgiler kurumdaki kaydınızla eşleşmiyor. Lütfen bilgilerinizi kontrol edin veya hasta kabul birimine başvurun.";

    public async Task<AppUser> RegisterAsync(PortalRegistrationInput input, CancellationToken ct = default)
    {
        var nationalId = input.NationalId.Trim();
        if (await users.FindByNameAsync(nationalId) is not null)
        {
            throw new DomainException("Bu T.C. Kimlik No ile zaten bir hesap var. Lütfen giriş yapın.");
        }

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.NationalId == nationalId, ct);
        if (patient is not null)
        {
            if (patient.UserId is not null)
            {
                throw new DomainException("Bu T.C. Kimlik No ile zaten bir hesap var. Lütfen giriş yapın.");
            }

            if (string.IsNullOrWhiteSpace(patient.Phone))
            {
                throw new DomainException("Kurumdaki kaydınızda telefon numarası bulunmadığı için online hesap açılamıyor. Lütfen hasta kabul biriminde telefonunuzu kaydettirin.");
            }

            var sameName = SearchNormalizer.Normalize($"{input.FirstName} {input.LastName}") == patient.SearchName;
            if (!sameName || patient.BirthDate != input.BirthDate || NormalizePhone(patient.Phone) != NormalizePhone(input.Phone))
            {
                throw new DomainException(MismatchMessage);
            }

            if (!string.IsNullOrWhiteSpace(input.Email) && string.IsNullOrWhiteSpace(patient.Email))
            {
                patient.Email = input.Email.Trim();
            }
        }
        else
        {
            patient = new Patient
            {
                NationalId = nationalId,
                FirstName = input.FirstName.Trim(),
                LastName = input.LastName.Trim(),
                BirthDate = input.BirthDate,
                Phone = input.Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
            };
            patient.RefreshSearchName();
            db.Patients.Add(patient);
        }

        var user = new AppUser
        {
            UserName = nationalId,
            Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
            FullName = $"{patient.FirstName} {patient.LastName}",
            IsActive = true,
        };

        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded)
        {
            throw new DomainException(string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        await users.AddToRoleAsync(user, Roles.Patient);
        patient.UserId = user.Id;
        patient.PortalConsentAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>The patient record behind the signed-in portal user.</summary>
    public async Task<Patient> GetPatientAsync(string? userId, CancellationToken ct = default)
        => (userId is null ? null : await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct))
           ?? throw new NotFoundException("Hasta kaydınız bulunamadı.");

    public async Task UpdateContactAsync(string? userId, string phone, string? email, string? address, CancellationToken ct = default)
    {
        var patient = (userId is null ? null : await db.Patients.FirstOrDefaultAsync(p => p.UserId == userId, ct))
            ?? throw new NotFoundException("Hasta kaydınız bulunamadı.");
        patient.Phone = phone.Trim();
        patient.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        patient.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        await db.SaveChangesAsync(ct);
    }

    internal static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length > 10 ? digits[^10..] : digits;
    }
}
