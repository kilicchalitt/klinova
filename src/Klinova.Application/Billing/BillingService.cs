using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Application.Visits;
using Klinova.Domain.Billing;
using Klinova.Domain.Common;
using Klinova.Domain.Visits;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Billing;

public sealed class PaymentInput
{
    [Display(Name = "İndirim (₺)")]
    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "{0} 0 veya daha büyük olmalıdır.")]
    public decimal Discount { get; set; }

    [Display(Name = "Ödeme Şekli")]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [Display(Name = "Açıklama")]
    [StringLength(FieldLengths.ShortText * 2, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Note { get; set; }
}

public sealed record PaymentListItem(
    int PaymentId,
    string ReceiptNumber,
    int VisitId,
    string PatientName,
    string PolyclinicName,
    DateTime PaidAtUtc,
    PaymentMethod Method,
    decimal NetAmount,
    string? ReceivedBy);

/// <summary>Cashier operations: outstanding visits, payments and receipts.</summary>
public sealed class BillingService(IAppDbContext db, IClock clock, ICurrentUser currentUser, VisitService visits)
{
    /// <summary>Completed visits that have services but no payment yet.</summary>
    public async Task<List<VisitListItem>> ListOutstandingAsync(CancellationToken ct = default)
    {
        var completed = await visits.ListAsync(new VisitQuery(Status: VisitStatus.Completed), ct);
        return completed.Where(v => !v.IsPaid && v.TotalAmount > 0)
            .OrderBy(v => v.VisitDate).ThenBy(v => v.QueueNumber)
            .ToList();
    }

    public async Task<Payment> CollectAsync(int visitId, PaymentInput input, CancellationToken ct = default)
    {
        var visit = await db.Visits
                        .Include(v => v.Items)
                        .Include(v => v.Payment)
                        .FirstOrDefaultAsync(v => v.Id == visitId, ct)
                    ?? throw NotFoundException.For("Başvuru", visitId);

        var payment = visit.RegisterPayment(
            decimal.Round(input.Discount, 2, MidpointRounding.AwayFromZero),
            input.Method,
            input.Note,
            currentUser.UserName,
            clock.UtcNow);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Unique index on Payments.VisitId: another cashier was faster.
            db.DiscardChanges();
            throw new DomainException("Bu başvurunun tahsilatı başka bir kullanıcı tarafından yapılmış.", ex);
        }

        return payment;
    }

    public async Task<List<PaymentListItem>> ListPaymentsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var fromUtc = clock.StartOfDayUtc(from);
        var toUtc = clock.StartOfDayUtc(to.AddDays(1));

        var rows = await db.Payments.AsNoTracking()
            .Where(p => p.PaidAtUtc >= fromUtc && p.PaidAtUtc < toUtc)
            .OrderByDescending(p => p.PaidAtUtc)
            .Join(
                db.Visits,
                p => p.VisitId,
                v => v.Id,
                (p, v) => new
                {
                    p.Id,
                    p.VisitId,
                    v.Patient!.FirstName,
                    v.Patient.LastName,
                    PolyclinicName = v.Polyclinic!.Name,
                    p.PaidAtUtc,
                    p.Method,
                    p.NetAmount,
                    p.ReceivedBy,
                })
            .ToListAsync(ct);

        return rows.Select(r => new PaymentListItem(
                r.Id,
                "MKB-" + r.Id.ToString("D8", System.Globalization.CultureInfo.InvariantCulture),
                r.VisitId,
                $"{r.FirstName} {r.LastName}",
                r.PolyclinicName,
                r.PaidAtUtc,
                r.Method,
                r.NetAmount,
                r.ReceivedBy))
            .ToList();
    }
}
