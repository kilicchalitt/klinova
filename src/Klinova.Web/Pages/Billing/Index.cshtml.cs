using Klinova.Application.Abstractions;
using Klinova.Application.Billing;
using Klinova.Application.Visits;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Billing;

public class IndexModel(BillingService billing, IClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? Date { get; set; }

    public List<VisitListItem> Outstanding { get; private set; } = [];

    public List<PaymentListItem> Payments { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Date ??= clock.Today;
        Outstanding = await billing.ListOutstandingAsync(ct);
        Payments = await billing.ListPaymentsAsync(Date.Value, Date.Value, ct);
    }
}
