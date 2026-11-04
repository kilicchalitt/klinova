using Klinova.Domain.Common;

namespace Klinova.Domain.Visits;

/// <summary>
/// A service performed during a visit. Code, name and price are copied from the
/// price list so that later price changes do not alter historical records.
/// </summary>
public class VisitItem : Entity
{
    private VisitItem()
    {
    }

    internal VisitItem(int medicalServiceId, string serviceCode, string serviceName, int quantity, decimal unitPrice, DateTime addedAtUtc)
    {
        MedicalServiceId = medicalServiceId;
        ServiceCode = serviceCode;
        ServiceName = serviceName;
        Quantity = quantity;
        UnitPrice = unitPrice;
        AddedAtUtc = addedAtUtc;
    }

    public int VisitId { get; private set; }

    public int MedicalServiceId { get; private set; }

    public string ServiceCode { get; private set; } = string.Empty;

    public string ServiceName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public DateTime AddedAtUtc { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
