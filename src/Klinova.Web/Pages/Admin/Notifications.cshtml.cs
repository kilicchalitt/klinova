using Klinova.Application.Abstractions;
using Klinova.Application.Notifications;
using Klinova.Domain.Notifications;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Web.Pages.Admin;

/// <summary>Outbox of SMS / e-mail messages with delivery status.</summary>
public class NotificationsModel(IAppDbContext db, NotificationDispatcher dispatcher) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public NotificationStatus? Status { get; set; }

    public List<Notification> Items { get; private set; } = [];

    public Dictionary<NotificationStatus, int> Counts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Counts = await db.Notifications.AsNoTracking().GroupBy(n => n.Status).ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
        Items = await db.Notifications.AsNoTracking()
            .Where(n => Status == null || n.Status == Status)
            .OrderByDescending(n => n.Id)
            .Take(200)
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostRetryAsync(long id, CancellationToken ct)
    {
        await dispatcher.RetryAsync(id, ct);
        this.Success("Bildirim yeniden gönderim kuyruğuna alındı.");
        return RedirectToPage(new { Status });
    }

    public async Task<IActionResult> OnPostDispatchAsync(CancellationToken ct)
    {
        var sent = await dispatcher.DispatchPendingAsync(ct: ct);
        this.Success($"{sent} bildirim gönderildi.");
        return RedirectToPage(new { Status });
    }
}
