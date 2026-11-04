using System.ComponentModel.DataAnnotations;

namespace Klinova.Domain.Visits;

public enum VisitStatus
{
    [Display(Name = "Sırada")]
    Waiting = 0,

    [Display(Name = "Muayenede")]
    InExamination = 1,

    [Display(Name = "Tamamlandı")]
    Completed = 2,

    [Display(Name = "İptal")]
    Cancelled = 3,
}
