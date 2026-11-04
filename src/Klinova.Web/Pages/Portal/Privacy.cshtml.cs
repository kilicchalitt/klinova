using Klinova.Application.Settings;
using Klinova.Domain.Settings;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Portal;

public class PrivacyModel(InstitutionService institution) : PageModel
{
    public InstitutionProfile Institution { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct) => Institution = await institution.GetAsync(ct);
}
