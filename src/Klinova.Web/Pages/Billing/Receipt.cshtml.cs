using Klinova.Application.Settings;
using Klinova.Application.Visits;
using Klinova.Domain.Settings;
using Klinova.Domain.Visits;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Billing;

public class ReceiptModel(VisitService visits, InstitutionService institution) : PageModel
{
    public Visit Visit { get; private set; } = default!;

    public InstitutionProfile Institution { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int visitId, CancellationToken ct)
    {
        Visit = await visits.GetDetailsAsync(visitId, ct);
        if (!Visit.IsPaid)
        {
            return RedirectToPage("Pay", new { visitId });
        }

        Institution = await institution.GetAsync(ct);
        return Page();
    }
}
