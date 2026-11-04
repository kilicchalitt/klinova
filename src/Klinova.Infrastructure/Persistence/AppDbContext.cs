using Klinova.Application.Abstractions;
using Klinova.Domain.Appointments;
using Klinova.Domain.Auditing;
using Klinova.Domain.Billing;
using Klinova.Domain.Notifications;
using Klinova.Domain.Organization;
using Klinova.Domain.Patients;
using Klinova.Domain.Scheduling;
using Klinova.Domain.Services;
using Klinova.Domain.Settings;
using Klinova.Domain.Visits;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Infrastructure.Persistence;

/// <summary>
/// Klinova database context. Each supported database provider has a thin
/// subclass (<see cref="SqliteAppDbContext"/>, <see cref="SqlServerAppDbContext"/>)
/// that owns its own migration history.
/// </summary>
public abstract class AppDbContext(DbContextOptions options) : IdentityDbContext<AppUser>(options), IAppDbContext
{
    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Polyclinic> Polyclinics => Set<Polyclinic>();

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<MedicalService> MedicalServices => Set<MedicalService>();

    public DbSet<Visit> Visits => Set<Visit>();

    public DbSet<VisitItem> VisitItems => Set<VisitItem>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<InstitutionProfile> InstitutionProfiles => Set<InstitutionProfile>();

    public DbSet<Facility> Facilities => Set<Facility>();

    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();

    public DbSet<DoctorLeave> DoctorLeaves => Set<DoctorLeave>();

    public DbSet<Holiday> Holidays => Set<Holiday>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public void DiscardChanges() => ChangeTracker.Clear();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<AppUser>(user =>
        {
            user.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}

/// <summary>SQLite flavour of the context (default, zero configuration).</summary>
public sealed class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : AppDbContext(options);

/// <summary>Microsoft SQL Server flavour of the context.</summary>
public sealed class SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options) : AppDbContext(options);
