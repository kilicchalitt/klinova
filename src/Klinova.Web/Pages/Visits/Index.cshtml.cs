using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Application.Visits;
using Klinova.Domain.Visits;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Visits;

public class IndexModel(VisitService visits, PolyclinicService polyclinics, Application.Abstractions.IClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? Date { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PolyclinicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public VisitStatus? Status { get; set; }

    public List<VisitListItem> Visits { get; private set; } = [];

    public List<LookupItem> Polyclinics { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Date ??= clock.Today;
        Polyclinics = await polyclinics.LookupAsync(activeOnly: false, ct);
        Visits = await visits.ListAsync(new VisitQuery(Date, PolyclinicId, Status: Status), ct);
    }
}
