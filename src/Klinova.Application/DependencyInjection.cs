using Klinova.Application.Auditing;
using Klinova.Application.Billing;
using Klinova.Application.Dashboard;
using Klinova.Application.Organization;
using Klinova.Application.Patients;
using Klinova.Application.Reports;
using Klinova.Application.Services;
using Klinova.Application.Settings;
using Klinova.Application.Visits;
using Microsoft.Extensions.DependencyInjection;

namespace Klinova.Application;

public static class DependencyInjection
{
    /// <summary>Registers the application services (use cases).</summary>
    public static IServiceCollection AddKlinovaApplication(this IServiceCollection services)
    {
        services.AddScoped<PatientService>();
        services.AddScoped<PolyclinicService>();
        services.AddScoped<DoctorService>();
        services.AddScoped<MedicalServiceCatalog>();
        services.AddScoped<VisitService>();
        services.AddScoped<BillingService>();
        services.AddScoped<ReportService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<InstitutionService>();
        services.AddScoped<AuditLogService>();
        return services;
    }
}
