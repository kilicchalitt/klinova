using Klinova.Application.Abstractions;
using Klinova.Application.Reports;
using Klinova.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Reports;

public class IndexModel(ReportService reports, IClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? To { get; set; }

    public RevenueReport? Report { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Normalize();
        try
        {
            Report = await reports.GetRevenueReportAsync(From!.Value, To!.Value, ct);
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        Normalize();
        var report = await reports.GetRevenueReportAsync(From!.Value, To!.Value, ct);
        var fileName = $"klinova-gelir-raporu-{From:yyyyMMdd}-{To:yyyyMMdd}.csv";
        return File(ReportService.ToCsv(report), "text/csv; charset=utf-8", fileName);
    }

    private void Normalize()
    {
        var today = clock.Today;
        To ??= today;
        From ??= new DateOnly(To.Value.Year, To.Value.Month, 1);
    }
}
