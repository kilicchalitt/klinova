using Klinova.Application.Abstractions;
using Klinova.Application.Appointments;
using Klinova.Application.Common;
using Klinova.Application.Scheduling;
using Klinova.Domain.Appointments;
using Klinova.Domain.Organization;
using Klinova.Domain.Patients;
using Klinova.Domain.Scheduling;
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
    IClock clock,
    IServiceProvider services,
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
        if (!await db.Facilities.AnyAsync(ct))
        {
            db.Facilities.Add(ReferenceData.DefaultFacility());
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Default facility created. Review it under 'Kurumlar ve Şubeler'.");
        }

        if (!await db.Polyclinics.AnyAsync(ct))
        {
            var facilityId = await db.Facilities.OrderBy(f => f.Id).Select(f => f.Id).FirstAsync(ct);
            db.Polyclinics.AddRange(ReferenceData.Polyclinics(facilityId));
            logger.LogInformation("Default polyclinics created.");
        }

        var year = clock.Today.Year;
        foreach (var y in new[] { year, year + 1 })
        {
            var start = new DateOnly(y, 1, 1);
            var end = new DateOnly(y, 12, 31);
            if (!await db.Holidays.AnyAsync(h => h.Date >= start && h.Date <= end, ct))
            {
                db.Holidays.AddRange(ReferenceData.NationalHolidays(y));
                logger.LogInformation("National holidays for {Year} created. Add religious holidays under 'Resmî Tatiller'.", y);
            }
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

        // No default and no generated password: secrets must never end up in logs.
        var seed = _options.Seed;
        if (string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            throw new SetupRequiredException(
                "Klinova has no administrator yet. Set the initial administrator password with the configuration key " +
                "'Klinova:Seed:AdminPassword' (environment variable Klinova__Seed__AdminPassword, or KLINOVA_ADMIN_PASSWORD " +
                "in the .env file when using docker compose) and start the application again. " +
                "The password must be changed at first sign-in.");
        }

        await CreateUserAsync(seed.AdminUserName, seed.AdminEmail, "Sistem Yöneticisi", seed.AdminPassword, Roles.Admin);
        logger.LogInformation("Initial administrator '{UserName}' created from configuration.", seed.AdminUserName);
    }

    private async Task SeedDemoDataAsync(CancellationToken ct)
    {
        if (await db.Doctors.AnyAsync(ct) || await db.Patients.AnyAsync(ct))
        {
            return;
        }

        var password = _options.Seed.DemoPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new SetupRequiredException(
                "Demo data is enabled but no demo password is configured. Set 'Klinova:Seed:DemoPassword' " +
                "(environment variable Klinova__Seed__DemoPassword, or KLINOVA_DEMO_PASSWORD in the .env file).");
        }

        logger.LogInformation("Seeding demo data…");

        await CreateUserAsync("kabul", "kabul@klinova.local", "Deniz Kabul", password, Roles.Reception, mustChangePassword: false);
        await CreateUserAsync("vezne", "vezne@klinova.local", "Ece Vezne", password, Roles.Cashier, mustChangePassword: false);
        var doctorUser = await CreateUserAsync("hekim", "hekim@klinova.local", "Dr. Selin Aksoy", password, Roles.Physician, mustChangePassword: false);

        var polyclinics = await db.Polyclinics.OrderBy(p => p.Id).ToListAsync(ct);
        var internal_ = polyclinics.First(p => p.Name == "Dahiliye");
        var ent = polyclinics.First(p => p.Name == "Kulak Burun Boğaz");
        var pediatrics = polyclinics.First(p => p.Name == "Çocuk Sağlığı ve Hastalıkları");

        // A second site shows the multi-facility structure (il / ilçe / kurum) of online booking.
        var branch = new Facility { Name = "Semt Polikliniği", City = "Ankara", District = "Keçiören", Phone = "0312 000 00 00" };
        db.Facilities.Add(branch);
        await db.SaveChangesAsync(ct);
        var branchInternal = new Polyclinic { FacilityId = branch.Id, Name = "Dahiliye", Location = "Zemin kat" };
        var branchEye = new Polyclinic { FacilityId = branch.Id, Name = "Göz Hastalıkları", Location = "1. kat" };
        db.Polyclinics.AddRange(branchInternal, branchEye);
        await db.SaveChangesAsync(ct);

        Doctor[] doctors =
        [
            new() { Title = "Uzm. Dr.", FirstName = "Selin", LastName = "Aksoy", PolyclinicId = internal_.Id, UserId = doctorUser.Id },
            new() { Title = "Uzm. Dr.", FirstName = "Murat", LastName = "Kaya", PolyclinicId = internal_.Id },
            new() { Title = "Op. Dr.", FirstName = "Leyla", LastName = "Demir", PolyclinicId = ent.Id },
            new() { Title = "Uzm. Dr.", FirstName = "Kerem", LastName = "Şahin", PolyclinicId = pediatrics.Id },
            new() { Title = "Uzm. Dr.", FirstName = "Emre", LastName = "Yalçın", PolyclinicId = branchInternal.Id },
            new() { Title = "Op. Dr.", FirstName = "Deniz", LastName = "Kurt", PolyclinicId = branchEye.Id },
        ];
        db.Doctors.AddRange(doctors);
        await db.SaveChangesAsync(ct);

        // Weekday working hours: 09:00–12:00 and 13:30–16:30.
        var today = clock.Today;
        foreach (var doctor in doctors)
        {
            var slot = doctor.PolyclinicId == pediatrics.Id ? 20 : 15;
            foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            {
                db.WorkSchedules.Add(new WorkSchedule(doctor.Id, day, new TimeOnly(9, 0), new TimeOnly(12, 0), slot, today.AddDays(-7), null, isOnlineBookable: true));
                db.WorkSchedules.Add(new WorkSchedule(doctor.Id, day, new TimeOnly(13, 30), new TimeOnly(16, 30), slot, today.AddDays(-7), null, isOnlineBookable: day != DayOfWeek.Friday));
            }
        }

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

        await SeedDemoAppointmentsAsync(patients, doctors, ct);
        await SeedDemoPortalPatientAsync(password, doctors[1], ct);

    }

    /// <summary>
    /// A ready-to-use patient portal account so that the portal can be tried without registering.
    /// 10000000146 is the widely published example number with valid check digits.
    /// </summary>
    private async Task SeedDemoPortalPatientAsync(string password, Doctor doctor, CancellationToken ct)
    {
        const string nationalId = "10000000146";
        if (await db.Patients.AnyAsync(p => p.NationalId == nationalId, ct))
        {
            return;
        }

        var user = await CreateUserAsync(nationalId, "hasta@klinova.local", "Elif Şahin", password, Roles.Patient, mustChangePassword: false);
        var patient = new Patient
        {
            FirstName = "Elif",
            LastName = "Şahin",
            NationalId = nationalId,
            Gender = Gender.Female,
            BirthDate = new DateOnly(1990, 5, 17),
            Phone = "0555 000 00 06",
            InsuranceType = InsuranceType.SocialSecurity,
            UserId = user.Id,
            PortalConsentAtUtc = clock.UtcNow,
        };
        patient.RefreshSearchName();
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);

        try
        {
            var availability = services.GetRequiredService<AvailabilityService>();
            var slots = await availability.GetFreeSlotsAsync(doctor.PolyclinicId, doctor.Id, clock.Today.AddDays(1), clock.Today.AddDays(10), online: true, ct);
            if (slots.Count > 3)
            {
                await services.GetRequiredService<AppointmentService>().BookAsync(
                    new BookingRequest { PatientId = patient.Id, DoctorId = doctor.Id, Date = slots[3].Date, StartTime = slots[3].Start, Note = "Kontrol randevusu." },
                    AppointmentChannel.Online,
                    ct);
            }
        }
        catch (Domain.Common.DomainException ex)
        {
            logger.LogWarning(ex, "Demo portal appointment could not be created.");
        }

        logger.LogInformation("Demo accounts ready. Staff: admin, kabul, hekim, vezne. Patient portal: T.C. {NationalId}.", nationalId);
    }

    /// <summary>Books a few appointments on the next working day so the calendars are not empty.</summary>
    private async Task SeedDemoAppointmentsAsync(Patient[] patients, Doctor[] doctors, CancellationToken ct)
    {
        var availability = services.GetRequiredService<AvailabilityService>();
        var appointments = services.GetRequiredService<AppointmentService>();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                var doctor = doctors[i];
                var slots = await availability.GetFreeSlotsAsync(doctor.PolyclinicId, doctor.Id, clock.Today.AddDays(1), clock.Today.AddDays(10), online: false, ct);
                if (slots.Count > i * 2)
                {
                    var slot = slots[i * 2];
                    await appointments.BookAsync(new BookingRequest { PatientId = patients[i].Id, DoctorId = doctor.Id, Date = slot.Date, StartTime = slot.Start, Note = i == 0 ? "Tahlil sonuçlarımı getireceğim." : null }, AppointmentChannel.Desk, ct);
                }
            }
        }
        catch (Domain.Common.DomainException ex)
        {
            logger.LogWarning(ex, "Demo appointments could not be created.");
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

/// <summary>Start-up cannot continue until the operator supplies a required setting.</summary>
public sealed class SetupRequiredException : Exception
{
    public SetupRequiredException()
    {
    }

    public SetupRequiredException(string message)
        : base(message)
    {
    }

    public SetupRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
