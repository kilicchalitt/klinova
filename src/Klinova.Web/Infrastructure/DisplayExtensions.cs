using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Klinova.Domain.Visits;

namespace Klinova.Web.Infrastructure;

public static class DisplayExtensions
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Returns the [Display(Name)] of an enum member.</summary>
    public static string DisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }

    public static string Money(this decimal amount) => amount.ToString("C2", Turkish);

    public static string ShortDate(this DateOnly date) => date.ToString("dd.MM.yyyy", Turkish);

    public static string ShortDate(this DateOnly? date) => date?.ShortDate() ?? "—";

    public static string BadgeClass(this VisitStatus status) => status switch
    {
        VisitStatus.Waiting => "text-bg-warning",
        VisitStatus.InExamination => "text-bg-primary",
        VisitStatus.Completed => "text-bg-success",
        VisitStatus.Cancelled => "text-bg-secondary",
        _ => "text-bg-light",
    };

    public static string OrDash(this string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
