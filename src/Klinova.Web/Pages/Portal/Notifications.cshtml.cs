using System.Security.Claims;
using Klinova.Application.Notifications;
using Klinova.Domain.Notifications;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Portal;

/// <summary>Messages for the patient (confirmations, reminders, cancellations, waiting list).</summary>
public class NotificationsModel(PortalAccounts accounts, PatientInbox inbox) : PageModel
{
    public List<Notification> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var patient = await accounts.GetPatientAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        Items = await inbox.ListAsync(patient.Id, ct: ct);

        // Opening the page marks the messages as read; the list keeps showing which ones were new.
        await inbox.MarkAllReadAsync(patient.Id, ct);
    }
}
