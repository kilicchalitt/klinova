using System.Security.Claims;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Application.Visits;
using Klinova.Domain.Organization;
using Klinova.Domain.Visits;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Worklist;

/// <summary>Physician work list: today's patients of a polyclinic and "call next".</summary>
public class IndexModel(VisitService visits, DoctorService doctors, PolyclinicService polyclinics, IClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? PolyclinicId { get; set; }

    public Doctor? CurrentDoctor { get; private set; }

    public List<LookupItem> Polyclinics { get; private set; } = [];

    public List<VisitListItem> Waiting { get; private set; } = [];

    public List<VisitListItem> InExamination { get; private set; } = [];

    public List<VisitListItem> Completed { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        CurrentDoctor = await doctors.FindByUserIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        Polyclinics = await polyclinics.LookupAsync(activeOnly: true, ct);
        PolyclinicId ??= CurrentDoctor?.PolyclinicId ?? Polyclinics.FirstOrDefault()?.Id;

        if (PolyclinicId is null)
        {
            return;
        }

        var ownDoctorId = CurrentDoctor?.PolyclinicId == PolyclinicId ? CurrentDoctor.Id : (int?)null;
        var today = await visits.ListAsync(new VisitQuery(clock.Today, PolyclinicId, ownDoctorId), ct);
        Waiting = today.Where(v => v.Status == VisitStatus.Waiting).ToList();
        InExamination = today.Where(v => v.Status == VisitStatus.InExamination).ToList();
        Completed = today.Where(v => v.Status == VisitStatus.Completed).OrderByDescending(v => v.QueueNumber).ToList();
    }

    public async Task<IActionResult> OnPostCallNextAsync(int polyclinicId, CancellationToken ct)
    {
        var doctor = await doctors.FindByUserIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        var doctorId = doctor?.PolyclinicId == polyclinicId ? doctor.Id : (int?)null;

        var visitId = await visits.CallNextAsync(polyclinicId, doctorId, ct);
        if (visitId is null)
        {
            this.Error("Sırada bekleyen hasta yok.");
            return RedirectToPage(new { polyclinicId });
        }

        return RedirectToPage("/Visits/Details", new { id = visitId });
    }
}
