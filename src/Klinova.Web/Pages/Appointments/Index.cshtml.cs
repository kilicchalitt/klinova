using System.Globalization;
using Klinova.Application.Abstractions;
using Klinova.Application.Appointments;
using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Domain.Appointments;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Appointments;

/// <summary>Daily appointment calendar for the front desk and physicians.</summary>
public class IndexModel(AppointmentService appointments, FacilityService facilities, PolyclinicService polyclinics, DoctorService doctors, IClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? Date { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FacilityId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PolyclinicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? DoctorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public AppointmentStatus? Status { get; set; }

    public List<AppointmentListItem> Items { get; private set; } = [];

    public List<LookupItem> Facilities { get; private set; } = [];

    public List<LookupItem> Polyclinics { get; private set; } = [];

    public List<LookupItem> Doctors { get; private set; } = [];

    public bool IsDesk => User.IsInAnyRole(Roles.Admin, Roles.Reception);

    public DateTime NowLocal => clock.ToLocal(clock.UtcNow);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Date ??= clock.Today;
        Facilities = await facilities.LookupAsync(activeOnly: false, ct: ct);
        Polyclinics = await polyclinics.LookupAsync(activeOnly: false, ct);
        Doctors = await doctors.LookupAsync(PolyclinicId, ct);

        // Physicians linked to a doctor record see their own calendar by default.
        if (DoctorId is null && PolyclinicId is null && User.IsInRole(Roles.Physician) && !User.IsInRole(Roles.Admin)
            && await doctors.FindByUserIdAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, ct) is { } own)
        {
            DoctorId = own.Id;
        }

        Items = await appointments.ListAsync(new AppointmentQuery(Date, FacilityId, PolyclinicId, DoctorId, Status), ct);
    }

    public async Task<IActionResult> OnPostCheckInAsync(int id, CancellationToken ct)
    {
        if (!IsDesk)
        {
            return Forbid();
        }

        var visitId = await appointments.CheckInAsync(id, ct);
        this.Success("Hasta kabul edildi ve sıraya alındı.");
        return RedirectToPage("/Visits/Details", new { id = visitId });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, string? reason, CancellationToken ct)
    {
        if (!IsDesk)
        {
            return Forbid();
        }

        await appointments.CancelAsync(id, CancellationSource.Staff, reason, ct: ct);
        this.Success("Randevu iptal edildi; hasta bilgilendirilecek.");
        return RedirectToPage(new { Date = Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), FacilityId, PolyclinicId, DoctorId, Status });
    }

    public async Task<IActionResult> OnPostNoShowAsync(int id, CancellationToken ct)
    {
        if (!IsDesk)
        {
            return Forbid();
        }

        await appointments.MarkNoShowAsync(id, ct);
        this.Success("Randevu 'gelmedi' olarak işaretlendi.");
        return RedirectToPage(new { Date = Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), FacilityId, PolyclinicId, DoctorId, Status });
    }
}
