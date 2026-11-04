using System.ComponentModel.DataAnnotations;

namespace Klinova.Domain.Billing;

public enum PaymentMethod
{
    [Display(Name = "Nakit")]
    Cash = 0,

    [Display(Name = "Kredi / Banka Kartı")]
    Card = 1,

    [Display(Name = "Havale / EFT")]
    BankTransfer = 2,

    [Display(Name = "Sigorta / Kurum")]
    Insurance = 3,
}
