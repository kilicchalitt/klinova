using System.Security.Claims;
using Klinova.Application.Abstractions;
using Klinova.Application.Appointments;
using Klinova.Application.Patients;
using Klinova.Domain.Appointments;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Portal;

public class AppointmentsModel(PortalAccounts accounts, AppointmentService appointments, WaitlistService waitlist, PatientService patients, IClock clock) : PageModel
{
    public List<AppointmentListItem> Upcoming { get; private set; } = [];

    public List<AppointmentListItem> Past { get; private set; } = [];

    public List<WaitlistListItem> Waiting { get; private set; } = [];

    public IReadOnlyList<PatientVisitSummary> Visits { get; private set; } = [];

    public Func<AppointmentListItem, bool> CanCancel { get; private set; } = _ => false;

    public async Task OnGetAsync(CancellationToken ct)
    {
        var patient = await accounts.GetPatientAsync(UserId, ct);
        var today = clock.Today;
        var all = await appointments.ListForPatientAsync(patient.Id, ct);
        Upcoming = all.Where(a => a.Status == AppointmentStatus.Booked && a.Date >= today).OrderBy(a => a.StartsAtLocal).ToList();
        Past = all.Except(Upcoming).Take(20).ToList();
        Waiting = await waitlist.ListAsync(patient.Id, openOnly: true, ct);
        Visits = (await patients.GetVisitHistoryAsync(patient.Id, ct)).Take(20).ToList();
        CanCancel = appointments.CanPatientCancel;
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, CancellationToken ct)
    {
        var patient = await accounts.GetPatientAsync(UserId, ct);
        await appointments.CancelAsync(id, CancellationSource.Patient, null, patient.Id, ct);
        this.Success("Randevunuz iptal edildi. Bilgilendirmeniz için teşekkür ederiz.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLeaveWaitlistAsync(int id, CancellationToken ct)
    {
        var patient = await accounts.GetPatientAsync(UserId, ct);
        await waitlist.WithdrawAsync(id, patient.Id, ct);
        this.Success("Bekleme listesinden çıkarıldınız.");
        return RedirectToPage();
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
