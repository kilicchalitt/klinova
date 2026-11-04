using Klinova.Application.Common;
using Klinova.Domain.Organization;
using Klinova.Domain.Patients;
using Klinova.Domain.Settings;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Klinova.Infrastructure.Persistence.Seeding;

/// <summary>
/// Applies migrations and creates the data a fresh installation needs: roles, the
/// initial administrator, the institution profile and default reference data.
/// Every step is idempotent and safe to run on each start-up.
/// </summary>
public sealed class DatabaseInitializer(
    AppDbContext db,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<KlinovaOptions> options,
    ILogger<DatabaseInitializer> logger)
{
    private readonly KlinovaOptions _options = options.Value;

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var initializer = ActivatorUtilities.CreateInstance<DatabaseInitializer>(scope.ServiceProvider);
        await initializer.RunAsync(ct);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        if (_options.ApplyMigrationsOnStartup)
        {
            logger.LogInformation("Applying database migrations…");
            await db.Database.MigrateAsync(ct);
        }

        await EnsureRolesAsync();
        await EnsureInstitutionProfileAsync(ct);
        await EnsureReferenceDataAsync(ct);
        await EnsureAdministratorAsync();

        if (_options.Seed.DemoData)
        {
            await SeedDemoDataAsync(ct);
        }
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                ThrowOnFailure(await roleManager.CreateAsync(new IdentityRole(role)), $"create role {role}");
            }
        }
    }

    private async Task EnsureInstitutionProfileAsync(CancellationToken ct)
    {
        if (!await db.InstitutionProfiles.AnyAsync(ct))
        {
            db.InstitutionProfiles.Add(new InstitutionProfile());
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureReferenceDataAsync(CancellationToken ct)
    {
        if (!await db.Polyclinics.AnyAsync(ct))
        {
            db.Polyclinics.AddRange(ReferenceData.Polyclinics());
            logger.LogInformation("Default polyclinics created.");
        }

        if (!await db.MedicalServices.AnyAsync(ct))
        {
            db.MedicalServices.AddRange(ReferenceData.Services());
            logger.LogInformation("Default price list created. Review the prices under 'Fiyat Listesi'.");
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureAdministratorAsync()
    {
        if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Count > 0)
        {
            return;
        }

        var seed = _options.Seed;
        var password = string.IsNullOrWhiteSpace(seed.AdminPassword) ? PasswordGenerator.Generate() : seed.AdminPassword;

        await CreateUserAsync(seed.AdminUserName, seed.AdminEmail, "Sistem Yöneticisi", password, Roles.Admin);

        if (string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            // Shown once so the installer can sign in; the password must be changed at first sign-in.
            logger.LogWarning(
                "Initial administrator created. User name: {UserName}  Temporary password: {Password}  (change it at first sign-in)",
                seed.AdminUserName,
                password);
        }
        else
        {
            logger.LogInformation("Initial administrator '{UserName}' created from configuration.", seed.AdminUserName);
        }
    }

    private async Task SeedDemoDataAsync(CancellationToken ct)
    {
        if (await db.Doctors.AnyAsync(ct) || await db.Patients.AnyAsync(ct))
        {
            return;
        }

        logger.LogInformation("Seeding demo data…");
        var password = string.IsNullOrWhiteSpace(_options.Seed.DemoPassword) ? PasswordGenerator.Generate() : _options.Seed.DemoPassword;

        await CreateUserAsync("kabul", "kabul@klinova.local", "Deniz Kabul", password, Roles.Reception, mustChangePassword: false);
        await CreateUserAsync("vezne", "vezne@klinova.local", "Ece Vezne", password, Roles.Cashier, mustChangePassword: false);
        var doctorUser = await CreateUserAsync("hekim", "hekim@klinova.local", "Dr. Selin Aksoy", password, Roles.Physician, mustChangePassword: false);

        var polyclinics = await db.Polyclinics.OrderBy(p => p.Id).ToListAsync(ct);
        var internal_ = polyclinics.First(p => p.Name == "Dahiliye");
        var ent = polyclinics.First(p => p.Name == "Kulak Burun Boğaz");
        var pediatrics = polyclinics.First(p => p.Name == "Çocuk Sağlığı ve Hastalıkları");

        db.Doctors.AddRange(
            new Doctor { Title = "Uzm. Dr.", FirstName = "Selin", LastName = "Aksoy", PolyclinicId = internal_.Id, UserId = doctorUser.Id },
            new Doctor { Title = "Uzm. Dr.", FirstName = "Murat", LastName = "Kaya", PolyclinicId = internal_.Id },
            new Doctor { Title = "Op. Dr.", FirstName = "Leyla", LastName = "Demir", PolyclinicId = ent.Id },
            new Doctor { Title = "Uzm. Dr.", FirstName = "Kerem", LastName = "Şahin", PolyclinicId = pediatrics.Id });

        // Demo patients intentionally have no national id: any valid-looking number could belong to a real person.
        Patient[] patients =
        [
            new() { FirstName = "Ayşe", LastName = "Yıldız", Gender = Gender.Female, BirthDate = new DateOnly(1985, 3, 12), BloodType = BloodType.APositive, InsuranceType = InsuranceType.SocialSecurity, Phone = "0555 000 00 01" },
            new() { FirstName = "Mehmet", LastName = "Öztürk", Gender = Gender.Male, BirthDate = new DateOnly(1972, 11, 2), BloodType = BloodType.ZeroPositive, InsuranceType = InsuranceType.SocialSecurity, MedicalAlerts = "Penisilin alerjisi" },
            new() { FirstName = "Zeynep", LastName = "Arslan", Gender = Gender.Female, BirthDate = new DateOnly(2018, 6, 25), InsuranceType = InsuranceType.SocialSecurity, EmergencyContactName = "Ali Arslan", EmergencyContactPhone = "0555 000 00 02" },
            new() { FirstName = "İbrahim", LastName = "Çelik", Gender = Gender.Male, BirthDate = new DateOnly(1990, 1, 30), InsuranceType = InsuranceType.PrivateInsurance, InsuranceInstitution = "Örnek Sigorta A.Ş." },
            new() { FirstName = "Gülşen", LastName = "Koç", Gender = Gender.Female, BirthDate = new DateOnly(1958, 9, 14), InsuranceType = InsuranceType.SelfPay, MedicalAlerts = "Tip 2 diyabet, hipertansiyon" },
        ];

        foreach (var patient in patients)
        {
            patient.RefreshSearchName();
        }

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync(ct);

        if (string.IsNullOrWhiteSpace(_options.Seed.DemoPassword))
        {
            logger.LogWarning("Demo users 'kabul', 'hekim' and 'vezne' created. Shared demo password: {Password}", password);
        }
    }

    private async Task<AppUser> CreateUserAsync(string userName, string email, string fullName, string password, string role, bool mustChangePassword = true)
    {
        var user = new AppUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            MustChangePassword = mustChangePassword,
        };

        ThrowOnFailure(await userManager.CreateAsync(user, password), $"create user {userName}");
        ThrowOnFailure(await userManager.AddToRoleAsync(user, role), $"assign role {role} to {userName}");
        return user;
    }

    private static void ThrowOnFailure(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not {operation}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
