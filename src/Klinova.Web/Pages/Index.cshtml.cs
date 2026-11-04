using Klinova.Application.Common;
using Klinova.Application.Dashboard;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages;

/// <summary>
/// Site root: staff see the dashboard; visitors and patients are sent to the patient portal.
/// </summary>
public class IndexModel(DashboardService dashboard) : PageModel
{
    public DashboardSummary Summary { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.IsInAnyRole([.. Roles.Staff]))
        {
            return RedirectToPage("/Portal/Index");
        }

        Summary = await dashboard.GetAsync(ct);
        return Page();
    }
}
