using System.ComponentModel.DataAnnotations;
using Klinova.Application.Common;
using Klinova.Application.Organization;
using Klinova.Application.Services;
using Klinova.Application.Visits;
using Klinova.Domain.Visits;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Visits;

public class DetailsModel(VisitService visits, DoctorService doctors, MedicalServiceCatalog catalog) : PageModel
{
    public Visit Visit { get; private set; } = default!;

    public List<MedicalServiceListItem> Services { get; private set; } = [];

    public List<LookupItem> PolyclinicDoctors { get; private set; } = [];

    [BindProperty]
    public ExaminationInput Examination { get; set; } = new();

    public bool CanExamine => User.IsInAnyRole(Roles.Admin, Roles.Physician);

    public bool CanManageFlow => User.IsInAnyRole(Roles.Admin, Roles.Physician, Roles.Reception);

    public bool CanCancel => User.IsInAnyRole(Roles.Admin, Roles.Reception);

    public bool CanCollect => User.IsInAnyRole(Roles.Admin, Roles.Cashier, Roles.Reception);

    public async Task OnGetAsync(int id, CancellationToken ct)
    {
        await LoadAsync(id, ct);
        Examination = new ExaminationInput { Complaint = Visit.Complaint, Diagnosis = Visit.Diagnosis, Notes = Visit.ExaminationNotes };
    }

    public async Task<IActionResult> OnPostExaminationAsync(int id, CancellationToken ct)
    {
        if (!CanExamine)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(id, ct);
            return Page();
        }

        await visits.RecordExaminationAsync(id, Examination, ct);
        this.Success("Muayene bilgileri kaydedildi.");
        return RedirectToPage(new { id });
    }

    public Task<IActionResult> OnPostStartAsync(int id, CancellationToken ct)
        => RunAsync(CanManageFlow, async () =>
        {
            var doctor = await doctors.FindByUserIdAsync(UserId, ct);
            await visits.StartAsync(id, doctor?.Id, ct);
        }, id, "Hasta muayeneye alındı.");

    public Task<IActionResult> OnPostReturnToQueueAsync(int id, CancellationToken ct)
        => RunAsync(CanManageFlow, () => visits.ReturnToQueueAsync(id, ct), id, "Hasta sıraya geri alındı.");

    public Task<IActionResult> OnPostCompleteAsync(int id, CancellationToken ct)
        => RunAsync(CanManageFlow, () => visits.CompleteAsync(id, ct), id, "Muayene tamamlandı.");

    public Task<IActionResult> OnPostReopenAsync(int id, CancellationToken ct)
        => RunAsync(CanManageFlow, () => visits.ReopenAsync(id, ct), id, "Başvuru yeniden açıldı.");

    public Task<IActionResult> OnPostCancelAsync(int id, [StringLength(200)] string? reason, CancellationToken ct)
        => RunAsync(CanCancel, () => visits.CancelAsync(id, reason, ct), id, "Başvuru iptal edildi.");

    public Task<IActionResult> OnPostAssignDoctorAsync(int id, int doctorId, CancellationToken ct)
        => RunAsync(CanCancel, () => visits.AssignDoctorAsync(id, doctorId, ct), id, "Hekim atandı.");

    public Task<IActionResult> OnPostAddServiceAsync(int id, int serviceId, int quantity, CancellationToken ct)
        => RunAsync(CanManageFlow, () => visits.AddServiceAsync(id, serviceId, quantity <= 0 ? 1 : quantity, ct), id, "Hizmet eklendi.");

    public Task<IActionResult> OnPostRemoveServiceAsync(int id, int itemId, CancellationToken ct)
        => RunAsync(CanManageFlow, () => visits.RemoveServiceAsync(id, itemId, ct), id, "Hizmet kaldırıldı.");

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private async Task<IActionResult> RunAsync(bool allowed, Func<Task> action, int id, string successMessage)
    {
        if (!allowed)
        {
            return Forbid();
        }

        await action();
        this.Success(successMessage);
        return RedirectToPage(new { id });
    }

    private async Task LoadAsync(int id, CancellationToken ct)
    {
        Visit = await visits.GetDetailsAsync(id, ct);
        Services = await catalog.ListAsync(activeOnly: true, ct);
        PolyclinicDoctors = await doctors.LookupAsync(Visit.PolyclinicId, ct);
    }
}
