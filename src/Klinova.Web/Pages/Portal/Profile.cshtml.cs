using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Klinova.Domain.Common;
using Klinova.Domain.Patients;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Portal;

public class ProfileModel(PortalAccounts accounts) : PageModel
{
    [BindProperty]
    public ContactInput Input { get; set; } = new();

    public Patient Patient { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Patient = await accounts.GetPatientAsync(UserId, ct);
        Input = new ContactInput { Phone = Patient.Phone ?? string.Empty, Email = Patient.Email, Address = Patient.Address };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Patient = await accounts.GetPatientAsync(UserId, ct);
            return Page();
        }

        await accounts.UpdateContactAsync(UserId, Input.Phone, Input.Email, Input.Address, ct);
        this.Success("İletişim bilgileriniz güncellendi.");
        return RedirectToPage();
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public sealed class ContactInput
    {
        [Display(Name = "Cep Telefonu")]
        [Required(ErrorMessage = "{0} zorunludur.")]
        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
        [StringLength(FieldLengths.Phone)]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "E-posta")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(FieldLengths.Email)]
        public string? Email { get; set; }

        [Display(Name = "Adres")]
        [StringLength(FieldLengths.Address)]
        public string? Address { get; set; }
    }
}
