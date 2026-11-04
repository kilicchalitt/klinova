using Klinova.Application.Organization;
using Klinova.Domain.Common;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Facilities;

public class EditModel(FacilityService facilities) : PageModel
{
    [BindProperty]
    public FacilityInput Input { get; set; } = new();

    public bool IsNew { get; private set; }

    public async Task OnGetAsync(int? id, CancellationToken ct)
    {
        IsNew = id is null;
        if (id is { } existing)
        {
            Input = await facilities.GetForEditAsync(existing, ct);
        }
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        IsNew = id is null;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            if (id is { } existing)
            {
                await facilities.UpdateAsync(existing, Input, ct);
            }
            else
            {
                await facilities.CreateAsync(Input, ct);
            }
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        this.Success($"{Input.Name} kaydedildi.");
        return RedirectToPage("Index");
    }
}
