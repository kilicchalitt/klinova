using Klinova.Application.Abstractions;
using Klinova.Application.Organization;
using Klinova.Application.Scheduling;
using Klinova.Domain.Common;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin;

/// <summary>Physician working hours ("çalışma cetveli") and leave.</summary>
public class SchedulesModel(ScheduleService schedules, DoctorService doctors, IClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? DoctorId { get; set; }

    [BindProperty]
    public WorkScheduleInput Block { get; set; } = new();

    [BindProperty]
    public LeaveInput Leave { get; set; } = new();

    public List<DoctorListItem> Doctors { get; private set; } = [];

    public DoctorScheduleView? Schedule { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Doctors = (await doctors.ListAsync(ct: ct)).Where(d => d.IsActive).ToList();
        DoctorId ??= Doctors.FirstOrDefault()?.Id;
        if (DoctorId is { } id)
        {
            Schedule = await schedules.GetDoctorScheduleAsync(id, ct);
        }

        Block.ValidFrom = clock.Today;
        Leave.StartDate = clock.Today;
        Leave.EndDate = clock.Today;
    }

    public async Task<IActionResult> OnPostAddBlockAsync(CancellationToken ct)
    {
        if (DoctorId is not { } id || !ModelState.IsValid)
        {
            this.Error("Çalışma bloğu bilgilerini kontrol edin.");
            return RedirectToPage(new { DoctorId });
        }

        try
        {
            var count = await schedules.AddScheduleAsync(id, Block, ct);
            this.Success($"{count} çalışma bloğu eklendi.");
        }
        catch (DomainException ex)
        {
            this.Error(ex.Message);
        }

        return RedirectToPage(new { DoctorId });
    }

    public async Task<IActionResult> OnPostDeleteBlockAsync(int scheduleId, CancellationToken ct)
    {
        await schedules.DeleteScheduleAsync(scheduleId, ct);
        this.Success("Çalışma bloğu silindi. Bu bloktaki mevcut randevular geçerliliğini korur.");
        return RedirectToPage(new { DoctorId });
    }

    public async Task<IActionResult> OnPostAddLeaveAsync(CancellationToken ct)
    {
        if (DoctorId is not { } id)
        {
            return RedirectToPage();
        }

        try
        {
            var affected = await schedules.AddLeaveAsync(id, Leave, ct);
            this.Success(affected == 0
                ? "İzin eklendi."
                : $"İzin eklendi. Bu tarihlerde {affected} aktif randevu var; Randevular ekranından değiştirmeyi veya iptal etmeyi unutmayın.");
        }
        catch (DomainException ex)
        {
            this.Error(ex.Message);
        }

        return RedirectToPage(new { DoctorId });
    }

    public async Task<IActionResult> OnPostDeleteLeaveAsync(int leaveId, CancellationToken ct)
    {
        await schedules.DeleteLeaveAsync(leaveId, ct);
        this.Success("İzin silindi.");
        return RedirectToPage(new { DoctorId });
    }
}
