using Klinova.Application.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Services;

public class IndexModel(MedicalServiceCatalog catalog) : PageModel
{
    public List<MedicalServiceListItem> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Items = await catalog.ListAsync(ct: ct);
}
