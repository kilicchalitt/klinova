using Klinova.Application.Settings;
using Klinova.Application.Visits;
using Klinova.Domain.Settings;
using Klinova.Domain.Visits;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Visits;

public class SlipModel(VisitService visits, InstitutionService institution) : PageModel
{
    public Visit Visit { get; private set; } = default!;

    public InstitutionProfile Institution { get; private set; } = default!;

    public async Task OnGetAsync(int id, CancellationToken ct)
    {
        Visit = await visits.GetDetailsAsync(id, ct);
        Institution = await institution.GetAsync(ct);
    }
}
