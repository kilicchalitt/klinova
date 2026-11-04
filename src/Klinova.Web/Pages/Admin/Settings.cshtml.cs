using Klinova.Application.Settings;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin;

public class SettingsModel(InstitutionService institution) : PageModel
{
    [BindProperty]
    public InstitutionInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct) => Input = await institution.GetForEditAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await institution.UpdateAsync(Input, ct);
        this.Success("Kurum bilgileri güncellendi.");
        return RedirectToPage();
    }
}
