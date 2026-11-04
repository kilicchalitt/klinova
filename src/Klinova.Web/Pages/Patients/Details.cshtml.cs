using Klinova.Application.Patients;
using Klinova.Domain.Patients;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Patients;

public class DetailsModel(PatientService patients, IAuthorizationService authorization) : PageModel
{
    public Patient Patient { get; private set; } = default!;

    public IReadOnlyList<PatientVisitSummary> Visits { get; private set; } = [];

    public async Task OnGetAsync(int id, CancellationToken ct)
    {
        Patient = await patients.GetAsync(id, ct);
        Visits = await patients.GetVisitHistoryAsync(id, ct);
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id, bool archived, CancellationToken ct)
    {
        if (!(await authorization.AuthorizeAsync(User, Policies.PatientRegistration)).Succeeded)
        {
            return Forbid();
        }

        await patients.SetArchivedAsync(id, archived, ct);
        this.Success(archived ? "Hasta arşive alındı." : "Hasta arşivden çıkarıldı.");
        return RedirectToPage(new { id });
    }
}
