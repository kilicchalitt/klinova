using Klinova.Application.Organization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Doctors;

public class IndexModel(DoctorService doctors) : PageModel
{
    public List<DoctorListItem> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Items = await doctors.ListAsync(ct: ct);
}
