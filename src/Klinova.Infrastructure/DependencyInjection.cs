using Klinova.Application.Abstractions;
using Klinova.Application.Appointments;
using Klinova.Application.Notifications;
using Klinova.Infrastructure.BackgroundJobs;
using Klinova.Infrastructure.Identity;
using Klinova.Infrastructure.Notifications;
using Klinova.Infrastructure.Persistence;
using Klinova.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klinova.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence, identity, auditing and system services.
    /// The database provider is selected with <c>Database:Provider</c>
    /// and the connection string <c>ConnectionStrings:Default</c>.
    /// </summary>
    public static IServiceCollection AddKlinovaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KlinovaOptions>().Bind(configuration.GetSection(KlinovaOptions.SectionName));
        var database = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Default' is not configured.");

        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        switch (database.Provider)
        {
            case DatabaseProvider.SqlServer:
                services.AddDbContext<SqlServerAppDbContext>((sp, o) => o
                    .UseSqlServer(connectionString)
                    .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqlServerAppDbContext>());
                break;

            default:
                EnsureSqliteDirectoryExists(connectionString);
                services.AddDbContext<SqliteAppDbContext>((sp, o) => o
                    .UseSqlite(connectionString)
                    // SQLite rebuilds tables to add foreign keys, which EF reports on every fresh install; expected and harmless.
                    .ConfigureWarnings(w => w.Ignore(RelationalEventId.NonTransactionalMigrationOperationWarning))
                    .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqliteAppDbContext>());
                break;
        }

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services
            .AddIdentity<AppUser, IdentityRole>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Lockout.AllowedForNewUsers = true;
                o.User.RequireUniqueEmail = false;
                o.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<TurkishIdentityErrorDescriber>();

        var keysPath = configuration[$"{KlinovaOptions.SectionName}:{nameof(KlinovaOptions.DataProtectionKeysPath)}"];
        var dataProtection = services.AddDataProtection().SetApplicationName("Klinova");
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(Directory.CreateDirectory(keysPath));
        }

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        AddAppointmentsAndNotifications(services, configuration);

        return services;
    }

    private static void AddAppointmentsAndNotifications(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppointmentPolicy>().Bind(configuration.GetSection(AppointmentPolicy.SectionName));
        services.AddOptions<NotificationOptions>().Bind(configuration.GetSection(NotificationOptions.SectionName));
        services.AddOptions<BackgroundJobOptions>().Bind(configuration.GetSection(BackgroundJobOptions.SectionName));

        var notifications = configuration.GetSection(NotificationOptions.SectionName).Get<NotificationOptions>() ?? new NotificationOptions();
        var smsConfigured = string.Equals(notifications.Sms.Provider, "Webhook", StringComparison.OrdinalIgnoreCase);
        services.Configure<NotificationDeliveryOptions>(o =>
        {
            o.SmsEnabled = smsConfigured;
            o.EmailEnabled = notifications.Email.Enabled;
        });
        if (notifications.Email.Enabled)
        {
            services.AddScoped<INotificationSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<INotificationSender, LoggingEmailSender>();
        }

        if (smsConfigured)
        {
            services.AddHttpClient<WebhookSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
            services.AddScoped<INotificationSender>(sp => sp.GetRequiredService<WebhookSmsSender>());
        }
        else
        {
            services.AddScoped<INotificationSender, LoggingSmsSender>();
        }

        var jobs = configuration.GetSection(BackgroundJobOptions.SectionName).Get<BackgroundJobOptions>() ?? new BackgroundJobOptions();
        if (jobs.Enabled)
        {
            services.AddHostedService<NotificationDispatchWorker>();
            services.AddHostedService<AppointmentMaintenanceWorker>();
        }
    }

    private static void EnsureSqliteDirectoryExists(string connectionString)
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        var directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(directory) && builder.Mode != Microsoft.Data.Sqlite.SqliteOpenMode.Memory)
        {
            Directory.CreateDirectory(directory);
        }
    }
}
