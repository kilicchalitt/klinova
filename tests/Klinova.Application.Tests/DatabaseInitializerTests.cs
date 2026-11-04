using Klinova.Infrastructure;
using Klinova.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klinova.Application.Tests;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "klinova-init-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ServiceProvider Build(Dictionary<string, string?> settings)
    {
        settings["ConnectionStrings:Default"] = $"Data Source={Path.Combine(_directory, "klinova.db")}";
        settings["Klinova:BackgroundJobs:Enabled"] = "false";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLogging()
            .AddKlinovaApplication()
            .AddKlinovaInfrastructure(configuration)
            .BuildServiceProvider();
    }

    [Fact]
    public async Task FirstStart_RequiresAnAdministratorPassword()
    {
        await using var services = Build([]);

        var ex = await Assert.ThrowsAsync<SetupRequiredException>(() => DatabaseInitializer.InitializeAsync(services));

        Assert.Contains("Klinova__Seed__AdminPassword", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DemoData_RequiresADemoPassword()
    {
        await using var services = Build(new() { ["Klinova:Seed:AdminPassword"] = "Admin-Test-2026", ["Klinova:Seed:DemoData"] = "true" });

        var ex = await Assert.ThrowsAsync<SetupRequiredException>(() => DatabaseInitializer.InitializeAsync(services));

        Assert.Contains("Klinova__Seed__DemoPassword", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstStart_WithPasswords_SeedsTheInstallation()
    {
        await using var services = Build(new() { ["Klinova:Seed:AdminPassword"] = "Admin-Test-2026", ["Klinova:Seed:DemoData"] = "true", ["Klinova:Seed:DemoPassword"] = "Demo-Test-2026" });

        await DatabaseInitializer.InitializeAsync(services);
        await DatabaseInitializer.InitializeAsync(services); // idempotent

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();
        Assert.Equal(2, db.Facilities.Count());
        Assert.Contains(db.Patients, p => p.NationalId == "10000000146" && p.UserId != null);
        Assert.NotEmpty(db.WorkSchedules);
        Assert.NotEmpty(db.Holidays);
    }
}
