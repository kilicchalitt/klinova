using Klinova.Application.Appointments;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Appointments;

public class WaitlistModel(WaitlistService waitlist) : PageModel
{
    public List<WaitlistListItem> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Items = await waitlist.ListAsync(openOnly: true, ct: ct);

    public async Task<IActionResult> OnPostWithdrawAsync(int id, CancellationToken ct)
    {
        await waitlist.WithdrawAsync(id, ct: ct);
        this.Success("Kayıt bekleme listesinden çıkarıldı.");
        return RedirectToPage();
    }
}
