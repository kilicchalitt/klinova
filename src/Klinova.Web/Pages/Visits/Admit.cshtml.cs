using Klinova.Application.Organization;
using Klinova.Application.Patients;
using Klinova.Application.Services;
using Klinova.Application.Visits;
using Klinova.Domain.Common;
using Klinova.Domain.Patients;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Visits;

public class AdmitModel(VisitService visits, PatientService patients, PolyclinicService polyclinics, DoctorService doctors, MedicalServiceCatalog catalog) : PageModel
{
    /// <summary>Service code pre-selected for new admissions (outpatient examination).</summary>
    public const string DefaultServiceCode = "MUA";

    [BindProperty]
    public AdmissionInput Input { get; set; } = new();

    public Patient Patient { get; private set; } = default!;

    public List<PolyclinicListItem> Polyclinics { get; private set; } = [];

    public List<DoctorListItem> Doctors { get; private set; } = [];

    public List<MedicalServiceListItem> Services { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int patientId, CancellationToken ct)
    {
        await LoadAsync(patientId, ct);
        if (Patient.IsArchived)
        {
            this.Error("Arşivlenmiş hasta için başvuru açılamaz.");
            return RedirectToPage("/Patients/Details", new { id = patientId });
        }

        Input.PatientId = patientId;
        Input.ServiceIds = Services.Where(s => s.Code == DefaultServiceCode).Select(s => s.Id).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int patientId, bool printSlip, CancellationToken ct)
    {
        Input.PatientId = patientId;
        if (!ModelState.IsValid)
        {
            await LoadAsync(patientId, ct);
            return Page();
        }

        try
        {
            var visit = await visits.AdmitAsync(Input, ct);
            this.Success($"Başvuru oluşturuldu. Sıra numarası: {visit.QueueNumber}");
            return printSlip
                ? RedirectToPage("Slip", new { id = visit.Id })
                : RedirectToPage("Details", new { id = visit.Id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(patientId, ct);
            return Page();
        }
    }

    private async Task LoadAsync(int patientId, CancellationToken ct)
    {
        Patient = await patients.GetAsync(patientId, ct);
        Polyclinics = (await polyclinics.ListAsync(ct)).Where(p => p.IsActive).ToList();
        Doctors = (await doctors.ListAsync(ct: ct)).Where(d => d.IsActive).ToList();
        Services = await catalog.ListAsync(activeOnly: true, ct);
    }
}
