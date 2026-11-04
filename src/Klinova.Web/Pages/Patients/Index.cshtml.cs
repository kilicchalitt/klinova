using Klinova.Application.Common;
using Klinova.Application.Patients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Patients;

public class IndexModel(PatientService patients) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Term { get; set; }

    [BindProperty(SupportsGet = true, Name = "archived")]
    public bool IncludeArchived { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<PatientListItem> Result { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Result = await patients.SearchAsync(new PatientSearchQuery(Term, IncludeArchived, PageNumber), ct);

        // A search that matches exactly one patient by file number or national id opens the record directly.
        if (!string.IsNullOrWhiteSpace(Term) && Result.TotalCount == 1 && Term.Trim().All(char.IsAsciiDigit))
        {
            return RedirectToPage("Details", new { id = Result.Items[0].Id });
        }

        return Page();
    }
}
