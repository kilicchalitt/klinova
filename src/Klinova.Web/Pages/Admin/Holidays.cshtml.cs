using Klinova.Application.Abstractions;
using Klinova.Application.Scheduling;
using Klinova.Domain.Common;
using Klinova.Domain.Scheduling;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin;

public class HolidaysModel(ScheduleService schedules, IClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty]
    public HolidayInput Input { get; set; } = new();

    public List<Holiday> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Year ??= clock.Today.Year;
        Items = await schedules.ListHolidaysAsync(Year.Value, ct);
        Input.Date = clock.Today;
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            this.Error("Tatil bilgilerini kontrol edin.");
            return RedirectToPage(new { Year });
        }

        try
        {
            await schedules.AddHolidayAsync(Input, ct);
            this.Success($"{Input.Name} eklendi. Bu tarihteki boş randevular kapatıldı.");
        }
        catch (DomainException ex)
        {
            this.Error(ex.Message);
        }

        return RedirectToPage(new { Year = Input.Date.Year });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        await schedules.DeleteHolidayAsync(id, ct);
        this.Success("Tatil silindi.");
        return RedirectToPage(new { Year });
    }
}
