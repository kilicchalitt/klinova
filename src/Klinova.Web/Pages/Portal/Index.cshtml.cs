using Klinova.Application.Appointments;
using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Portal;

public class IndexModel(FacilityService facilities, PortalAccounts accounts, AppointmentService appointments, Application.Abstractions.IClock clock) : PageModel
{
    public List<FacilityListItem> Facilities { get; private set; } = [];

    public string? PatientName { get; private set; }

    public List<AppointmentListItem> Upcoming { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (User.IsInAnyRole([.. Roles.Staff]))
        {
            return RedirectToPage("/Index");
        }

        Facilities = (await facilities.ListAsync(ct)).Where(f => f.IsActive).ToList();
        if (User.IsInRole(Roles.Patient))
        {
            var patient = await accounts.GetPatientAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, ct);
            PatientName = patient.FirstName;
            var today = clock.Today;
            Upcoming = (await appointments.ListForPatientAsync(patient.Id, ct))
                .Where(a => a.Status == Domain.Appointments.AppointmentStatus.Booked && a.Date >= today)
                .OrderBy(a => a.StartsAtLocal)
                .ToList();
        }

        return Page();
    }
}
