using Klinova.Application.Services;
using Klinova.Domain.Common;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Services;

public class EditModel(MedicalServiceCatalog catalog) : PageModel
{
    [BindProperty]
    public MedicalServiceInput Input { get; set; } = new();

    public bool IsNew { get; private set; }

    public async Task OnGetAsync(int? id, CancellationToken ct)
    {
        IsNew = id is null;
        if (id is { } existing)
        {
            Input = await catalog.GetForEditAsync(existing, ct);
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
                await catalog.UpdateAsync(existing, Input, ct);
            }
            else
            {
                await catalog.CreateAsync(Input, ct);
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
