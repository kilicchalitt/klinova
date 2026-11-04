using Klinova.Application.Dashboard;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages;

public class IndexModel(DashboardService dashboard) : PageModel
{
    public DashboardSummary Summary { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct) => Summary = await dashboard.GetAsync(ct);
}
