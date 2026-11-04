using Klinova.Domain.Auditing;
using Klinova.Domain.Billing;
using Klinova.Domain.Organization;
using Klinova.Domain.Patients;
using Klinova.Domain.Services;
using Klinova.Domain.Settings;
using Klinova.Domain.Visits;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Abstractions;

/// <summary>Unit of work over the Klinova database used by application services.</summary>
public interface IAppDbContext
{
    DbSet<Patient> Patients { get; }

    DbSet<Polyclinic> Polyclinics { get; }

    DbSet<Doctor> Doctors { get; }

    DbSet<MedicalService> MedicalServices { get; }

    DbSet<Visit> Visits { get; }

    DbSet<VisitItem> VisitItems { get; }

    DbSet<Payment> Payments { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<InstitutionProfile> InstitutionProfiles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Discards pending changes, e.g. before retrying an operation after a conflict.</summary>
    void DiscardChanges();
}
