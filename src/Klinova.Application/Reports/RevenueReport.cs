using Klinova.Domain.Billing;

namespace Klinova.Application.Reports;

public sealed record RevenueBreakdown(string Label, int Count, decimal Amount);

public sealed record DailyRevenue(DateOnly Day, int PaymentCount, decimal GrossAmount, decimal Discount, decimal NetAmount);

public sealed record RevenueReport(
    DateOnly From,
    DateOnly To,
    int VisitCount,
    int CompletedVisitCount,
    int CancelledVisitCount,
    int PaymentCount,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    IReadOnlyList<DailyRevenue> Daily,
    IReadOnlyList<RevenueBreakdown> ByPolyclinic,
    IReadOnlyList<RevenueBreakdown> ByDoctor,
    IReadOnlyList<RevenueBreakdown> ByService,
    IReadOnlyDictionary<PaymentMethod, decimal> ByPaymentMethod);
