namespace Klinova.Application.Common;

/// <summary>Application roles. Names are stored in the database and must not be renamed.</summary>
public static class Roles
{
    /// <summary>Full access including users, settings and the audit log.</summary>
    public const string Admin = "Admin";

    /// <summary>Patient registration and admission ("hasta kabul").</summary>
    public const string Reception = "Reception";

    /// <summary>Physicians: work list and examinations.</summary>
    public const string Physician = "Physician";

    /// <summary>Cashier ("vezne"): payments and revenue reports.</summary>
    public const string Cashier = "Cashier";

    /// <summary>Patients using the online portal. Never combined with a staff role.</summary>
    public const string Patient = "Patient";

    /// <summary>Roles of institution staff, i.e. everyone who may use the staff application.</summary>
    public static readonly IReadOnlyList<string> Staff = [Admin, Reception, Physician, Cashier];

    /// <summary>Every role known to the application.</summary>
    public static readonly IReadOnlyList<string> All = [Admin, Reception, Physician, Cashier, Patient];

    public static string DisplayName(string role) => role switch
    {
        Admin => "Yönetici",
        Reception => "Hasta Kabul",
        Physician => "Hekim",
        Cashier => "Vezne",
        Patient => "Hasta",
        _ => role,
    };
}
