using Klinova.Application.Organization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Polyclinics;

public class IndexModel(PolyclinicService polyclinics) : PageModel
{
    public List<PolyclinicListItem> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Items = await polyclinics.ListAsync(ct);
}
