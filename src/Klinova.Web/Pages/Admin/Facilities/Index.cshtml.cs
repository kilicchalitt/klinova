using Klinova.Application.Organization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Facilities;

public class IndexModel(FacilityService facilities) : PageModel
{
    public List<FacilityListItem> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Items = await facilities.ListAsync(ct);
}
