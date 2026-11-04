using System.ComponentModel.DataAnnotations;

namespace Klinova.Domain.Appointments;

public enum AppointmentStatus
{
    [Display(Name = "Aktif")]
    Booked = 0,

    [Display(Name = "Geldi")]
    CheckedIn = 1,

    [Display(Name = "İptal")]
    Cancelled = 2,

    [Display(Name = "Gelmedi")]
    NoShow = 3,
}

public enum AppointmentChannel
{
    [Display(Name = "Hasta Kabul / Telefon")]
    Desk = 0,

    [Display(Name = "Online (Hasta Portalı)")]
    Online = 1,
}

public enum CancellationSource
{
    [Display(Name = "Hasta")]
    Patient = 0,

    [Display(Name = "Kurum")]
    Staff = 1,

    [Display(Name = "Sistem")]
    System = 2,
}

public enum WaitlistStatus
{
    [Display(Name = "Bekliyor")]
    Active = 0,

    [Display(Name = "Bilgilendirildi")]
    Notified = 1,

    [Display(Name = "Randevu Alındı")]
    Fulfilled = 2,

    [Display(Name = "Vazgeçildi")]
    Cancelled = 3,

    [Display(Name = "Süresi Doldu")]
    Expired = 4,
}
