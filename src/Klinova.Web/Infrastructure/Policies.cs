using Klinova.Application.Common;

namespace Klinova.Web.Infrastructure;

/// <summary>Authorisation policies mapping application areas to roles.</summary>
public static class Policies
{
    public const string Administration = nameof(Administration);
    public const string PatientRegistration = nameof(PatientRegistration);
    public const string ClinicalWork = nameof(ClinicalWork);
    public const string Billing = nameof(Billing);
    public const string Reporting = nameof(Reporting);

    public static readonly IReadOnlyDictionary<string, string[]> Definitions = new Dictionary<string, string[]>
    {
        [Administration] = [Roles.Admin],
        [PatientRegistration] = [Roles.Admin, Roles.Reception],
        [ClinicalWork] = [Roles.Admin, Roles.Physician],
        [Billing] = [Roles.Admin, Roles.Cashier, Roles.Reception],
        [Reporting] = [Roles.Admin, Roles.Cashier],
    };
}
