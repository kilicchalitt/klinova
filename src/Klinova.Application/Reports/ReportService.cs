using System.Globalization;
using System.Text;
using Klinova.Application.Abstractions;
using Klinova.Application.Organization;
using Klinova.Domain.Common;
using Klinova.Domain.Visits;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Reports;

/// <summary>
/// Financial and operational reports. Amounts are aggregated in memory after
/// filtering by date so that results are identical on every database provider
/// (SQLite cannot aggregate <see cref="decimal"/> columns natively).
/// </summary>
public sealed class ReportService(IAppDbContext db, IClock clock)
{
    public const int MaxRangeInDays = 366;

    public async Task<RevenueReport> GetRevenueReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from)
        {
            throw new DomainException("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        }

        if (to.DayNumber - from.DayNumber >= MaxRangeInDays)
        {
            throw new DomainException($"Rapor aralığı en fazla {MaxRangeInDays} gün olabilir.");
        }

        var fromUtc = clock.StartOfDayUtc(from);
        var toUtc = clock.StartOfDayUtc(to.AddDays(1));

        var statusCounts = await db.Visits.AsNoTracking()
            .Where(v => v.VisitDate >= from && v.VisitDate <= to)
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.PaidAtUtc >= fromUtc && p.PaidAtUtc < toUtc)
            .Join(
                db.Visits,
                p => p.VisitId,
                v => v.Id,
                (p, v) => new
                {
                    p.VisitId,
                    p.PaidAtUtc,
                    p.GrossAmount,
                    p.Discount,
                    p.NetAmount,
                    p.Method,
                    PolyclinicName = v.Polyclinic!.Name,
                    DoctorTitle = v.Doctor != null ? v.Doctor.Title : null,
                    DoctorFirst = v.Doctor != null ? v.Doctor.FirstName : null,
                    DoctorLast = v.Doctor != null ? v.Doctor.LastName : null,
                })
            .ToListAsync(ct);

        var paidVisitIds = payments.Select(p => p.VisitId).ToList();
        var lines = await db.VisitItems.AsNoTracking()
            .Where(i => paidVisitIds.Contains(i.VisitId))
            .Select(i => new { i.VisitId, i.ServiceName, i.Quantity, i.UnitPrice })
            .ToListAsync(ct);

        // Net revenue per service distributes each visit's discount proportionally.
        var discountRatio = payments.ToDictionary(p => p.VisitId, p => p.GrossAmount == 0 ? 0m : p.NetAmount / p.GrossAmount);
        var byService = lines
            .GroupBy(l => l.ServiceName)
            .Select(g => new RevenueBreakdown(
                g.Key,
                g.Sum(l => l.Quantity),
                decimal.Round(g.Sum(l => l.Quantity * l.UnitPrice * discountRatio[l.VisitId]), 2)))
            .OrderByDescending(r => r.Amount)
            .ToList();

        var daily = payments
            .GroupBy(p => DateOnly.FromDateTime(clock.ToLocal(p.PaidAtUtc)))
            .Select(g => new DailyRevenue(g.Key, g.Count(), g.Sum(p => p.GrossAmount), g.Sum(p => p.Discount), g.Sum(p => p.NetAmount)))
            .OrderBy(d => d.Day)
            .ToList();

        var byPolyclinic = payments
            .GroupBy(p => p.PolyclinicName)
            .Select(g => new RevenueBreakdown(g.Key, g.Count(), g.Sum(p => p.NetAmount)))
            .OrderByDescending(r => r.Amount)
            .ToList();

        var byDoctor = payments
            .GroupBy(p => p.DoctorFirst is null ? "Hekim atanmamış" : DoctorService.FullName(p.DoctorTitle, p.DoctorFirst, p.DoctorLast!))
            .Select(g => new RevenueBreakdown(g.Key, g.Count(), g.Sum(p => p.NetAmount)))
            .OrderByDescending(r => r.Amount)
            .ToList();

        var byMethod = payments
            .GroupBy(p => p.Method)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.NetAmount));

        int CountOf(VisitStatus status) => statusCounts.FirstOrDefault(s => s.Status == status)?.Count ?? 0;

        return new RevenueReport(
            from,
            to,
            statusCounts.Sum(s => s.Count),
            CountOf(VisitStatus.Completed),
            CountOf(VisitStatus.Cancelled),
            payments.Count,
            payments.Sum(p => p.GrossAmount),
            payments.Sum(p => p.Discount),
            payments.Sum(p => p.NetAmount),
            daily,
            byPolyclinic,
            byDoctor,
            byService,
            byMethod);
    }

    /// <summary>Exports the daily totals and breakdowns as CSV (semicolon separated, UTF-8 with BOM for Excel).</summary>
    public static byte[] ToCsv(RevenueReport report)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var sb = new StringBuilder();

        sb.AppendLine(CultureInfo.InvariantCulture, $"Gelir Raporu;{report.From:dd.MM.yyyy};{report.To:dd.MM.yyyy}");
        sb.AppendLine();
        sb.AppendLine("Gün;Tahsilat Sayısı;Brüt;İndirim;Net");
        foreach (var d in report.Daily)
        {
            sb.AppendLine(string.Join(';', d.Day.ToString("dd.MM.yyyy", tr), d.PaymentCount.ToString(tr), d.GrossAmount.ToString("N2", tr), d.Discount.ToString("N2", tr), d.NetAmount.ToString("N2", tr)));
        }

        sb.AppendLine(string.Join(';', "TOPLAM", report.PaymentCount.ToString(tr), report.GrossAmount.ToString("N2", tr), report.DiscountAmount.ToString("N2", tr), report.NetAmount.ToString("N2", tr)));

        AppendBreakdown(sb, "Poliklinik", "Tahsilat Sayısı", report.ByPolyclinic, tr);
        AppendBreakdown(sb, "Hekim", "Tahsilat Sayısı", report.ByDoctor, tr);
        AppendBreakdown(sb, "Hizmet", "Adet", report.ByService, tr);

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        return [.. preamble, .. body];
    }

    private static void AppendBreakdown(StringBuilder sb, string title, string countTitle, IEnumerable<RevenueBreakdown> rows, CultureInfo culture)
    {
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{title};{countTitle};Net Tutar");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(';', Escape(r.Label), r.Count.ToString(culture), r.Amount.ToString("N2", culture)));
        }
    }

    private static string Escape(string value)
    {
        // Prevent CSV/formula injection when the file is opened in a spreadsheet.
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0], StringComparison.Ordinal))
        {
            value = "'" + value;
        }

        return value.Contains(';', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
