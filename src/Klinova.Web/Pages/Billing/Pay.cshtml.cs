using Klinova.Application.Billing;
using Klinova.Application.Visits;
using Klinova.Domain.Common;
using Klinova.Domain.Visits;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Billing;

public class PayModel(BillingService billing, VisitService visits) : PageModel
{
    [BindProperty]
    public PaymentInput Input { get; set; } = new();

    public Visit Visit { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int visitId, CancellationToken ct)
    {
        Visit = await visits.GetDetailsAsync(visitId, ct);
        if (Visit.IsPaid)
        {
            return RedirectToPage("Receipt", new { visitId });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int visitId, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Visit = await visits.GetDetailsAsync(visitId, ct);
            return Page();
        }

        try
        {
            var payment = await billing.CollectAsync(visitId, Input, ct);
            this.Success($"Tahsilat tamamlandı. Makbuz No: {payment.ReceiptNumber}");
            return RedirectToPage("Receipt", new { visitId });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            Visit = await visits.GetDetailsAsync(visitId, ct);
            return Page();
        }
    }
}
