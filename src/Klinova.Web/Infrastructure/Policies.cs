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
    public const string AppointmentDesk = nameof(AppointmentDesk);
    public const string AppointmentView = nameof(AppointmentView);

    /// <summary>Any member of staff; the fallback policy for every page that does not opt out.</summary>
    public const string Staff = nameof(Staff);

    /// <summary>Patients using the online portal.</summary>
    public const string Patient = nameof(Patient);

    public static readonly IReadOnlyDictionary<string, string[]> Definitions = new Dictionary<string, string[]>
    {
        [Administration] = [Roles.Admin],
        [PatientRegistration] = [Roles.Admin, Roles.Reception],
        [ClinicalWork] = [Roles.Admin, Roles.Physician],
        [Billing] = [Roles.Admin, Roles.Cashier, Roles.Reception],
        [Reporting] = [Roles.Admin, Roles.Cashier],
        [AppointmentDesk] = [Roles.Admin, Roles.Reception],
        [AppointmentView] = [Roles.Admin, Roles.Reception, Roles.Physician],
        [Staff] = [.. Roles.Staff],
        [Patient] = [Roles.Patient],
    };
}
