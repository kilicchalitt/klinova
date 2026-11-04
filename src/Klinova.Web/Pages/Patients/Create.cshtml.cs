using Klinova.Application.Patients;
using Klinova.Domain.Common;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Patients;

public class CreateModel(PatientService patients) : PageModel
{
    [BindProperty]
    public PatientInput Input { get; set; } = new();

    /// <summary>When set, the patient is admitted right after registration.</summary>
    [BindProperty]
    public bool AdmitAfterSave { get; set; } = true;

    public void OnGet(string? nationalId, string? name)
    {
        Input.NationalId = nationalId;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Input.FirstName = string.Join(' ', parts.Length > 1 ? parts[..^1] : parts);
            Input.LastName = parts.Length > 1 ? parts[^1] : string.Empty;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        int id;
        try
        {
            id = await patients.RegisterAsync(Input, ct);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        this.Success($"{Input.FirstName} {Input.LastName} kaydedildi. Dosya No: {Domain.Patients.Patient.FormatFileNumber(id)}");
        return AdmitAfterSave
            ? RedirectToPage("/Visits/Admit", new { patientId = id })
            : RedirectToPage("Details", new { id });
    }
}
