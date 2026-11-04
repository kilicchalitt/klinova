using Klinova.Application.Abstractions;
using Klinova.Infrastructure.Identity;
using Klinova.Infrastructure.Persistence;
using Klinova.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        return services;
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
