using System.Globalization;
using Klinova.Domain.Common;

namespace Klinova.Domain.Billing;

/// <summary>Collection of the amount due for a completed visit ("tahsilat").</summary>
public class Payment : Entity
{
    private Payment()
    {
    }

    internal Payment(int visitId, decimal grossAmount, decimal discount, PaymentMethod method, string? note, string? receivedBy, DateTime paidAtUtc)
    {
        VisitId = visitId;
        GrossAmount = grossAmount;
        Discount = discount;
        NetAmount = grossAmount - discount;
        Method = method;
        Note = note;
        ReceivedBy = receivedBy;
        PaidAtUtc = paidAtUtc;
    }

    public int VisitId { get; private set; }

    public decimal GrossAmount { get; private set; }

    public decimal Discount { get; private set; }

    public decimal NetAmount { get; private set; }

    public PaymentMethod Method { get; private set; }

    public string? Note { get; private set; }

    public string? ReceivedBy { get; private set; }

    public DateTime PaidAtUtc { get; private set; }

    /// <summary>Receipt number printed on the payment slip, e.g. "MKB-00000042".</summary>
    public string ReceiptNumber => "MKB-" + Id.ToString("D8", CultureInfo.InvariantCulture);
}
