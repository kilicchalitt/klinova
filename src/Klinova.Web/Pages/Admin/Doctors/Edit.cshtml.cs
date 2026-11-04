using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Domain.Common;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Doctors;

public class EditModel(DoctorService doctors, PolyclinicService polyclinics, UserAdministration users) : PageModel
{
    [BindProperty]
    public DoctorInput Input { get; set; } = new();

    public bool IsNew { get; private set; }

    public List<LookupItem> Polyclinics { get; private set; } = [];

    public List<LookupUser> Accounts { get; private set; } = [];

    public async Task OnGetAsync(int? id, CancellationToken ct)
    {
        IsNew = id is null;
        if (id is { } existing)
        {
            Input = await doctors.GetForEditAsync(existing, ct);
        }

        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        IsNew = id is null;
        if (!ModelState.IsValid)
        {
            await LoadAsync(ct);
            return Page();
        }

        try
        {
            if (id is { } existing)
            {
                await doctors.UpdateAsync(existing, Input, ct);
            }
            else
            {
                await doctors.CreateAsync(Input, ct);
            }
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(ct);
            return Page();
        }

        this.Success($"{Input.FirstName} {Input.LastName} kaydedildi.");
        return RedirectToPage("Index");
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Polyclinics = await polyclinics.LookupAsync(activeOnly: false, ct);
        Accounts = await users.PhysicianAccountsAsync();
    }
}
