namespace Klinova.Infrastructure;

/// <summary>Application settings bound from the "Klinova" configuration section.</summary>
public sealed class KlinovaOptions
{
    public const string SectionName = "Klinova";

    /// <summary>IANA time zone of the institution; determines business days and queue resets.</summary>
    public string TimeZone { get; set; } = "Europe/Istanbul";

    /// <summary>Apply pending EF Core migrations at start-up.</summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;

    /// <summary>Folder for ASP.NET Core data-protection keys (cookie encryption). Must be persistent.</summary>
    public string? DataProtectionKeysPath { get; set; }

    public SeedOptions Seed { get; set; } = new();
}

public sealed class SeedOptions
{
    /// <summary>User name of the initial administrator account.</summary>
    public string AdminUserName { get; set; } = "admin";

    public string AdminEmail { get; set; } = "admin@klinova.local";

    /// <summary>
    /// Initial administrator password; required while no administrator exists. It is never
    /// logged and must be changed at first sign-in.
    /// </summary>
    public string? AdminPassword { get; set; }

    /// <summary>Create sample polyclinics, physicians, services, patients and demo users.</summary>
    public bool DemoData { get; set; }

    /// <summary>Password for the demo users and the demo patient; required when <see cref="DemoData"/> is enabled.</summary>
    public string? DemoPassword { get; set; }
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"Sqlite" (default) or "SqlServer".</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;
}

public enum DatabaseProvider
{
    Sqlite,
    SqlServer,
}
