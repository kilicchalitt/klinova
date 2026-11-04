using Klinova.Application.Organization;
using Klinova.Domain.Common;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Polyclinics;

public class EditModel(PolyclinicService polyclinics) : PageModel
{
    [BindProperty]
    public PolyclinicInput Input { get; set; } = new();

    public bool IsNew { get; private set; }

    public async Task OnGetAsync(int? id, CancellationToken ct)
    {
        IsNew = id is null;
        if (id is { } existing)
        {
            Input = await polyclinics.GetForEditAsync(existing, ct);
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
                await polyclinics.UpdateAsync(existing, Input, ct);
            }
            else
            {
                await polyclinics.CreateAsync(Input, ct);
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
