using Klinova.Application.Patients;
using Klinova.Domain.Common;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Patients;

public class EditModel(PatientService patients) : PageModel
{
    [BindProperty]
    public PatientInput Input { get; set; } = new();

    public string FileNumber { get; private set; } = string.Empty;

    public async Task OnGetAsync(int id, CancellationToken ct)
    {
        var patient = await patients.GetAsync(id, ct);
        Input = PatientInput.FromEntity(patient);
        FileNumber = patient.FileNumber;
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        FileNumber = Domain.Patients.Patient.FormatFileNumber(id);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await patients.UpdateAsync(id, Input, ct);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        this.Success("Hasta bilgileri güncellendi.");
        return RedirectToPage("Details", new { id });
    }
}
