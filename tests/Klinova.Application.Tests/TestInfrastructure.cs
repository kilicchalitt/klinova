using Klinova.Application.Abstractions;
using Klinova.Domain.Organization;
using Klinova.Domain.Patients;
using Klinova.Domain.Services;
using Klinova.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Tests;

internal sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 4, 7, 0, 0, DateTimeKind.Utc);

    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public DateOnly Today => DateOnly.FromDateTime(ToLocal(UtcNow));

    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone);

    public DateTime StartOfDayUtc(DateOnly day)
        => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), TimeZone);
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public string? UserId { get; set; } = "user-1";

    public string? UserName { get; set; } = "tester";

    public bool IsAuthenticated => UserId is not null;

    public bool IsInRole(string role) => true;
}

/// <summary>An isolated, migrated in-memory SQLite database per test.</summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var context = CreateContext();
        context.Database.Migrate();
    }

    public FakeClock Clock { get; } = new();

    public FakeCurrentUser User { get; } = new();

    /// <summary>Creates a fresh context, like a new HTTP request would get.</summary>
    public SqliteAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(User, Clock))
            .Options;
        return new SqliteAppDbContext(options);
    }

    public async Task<(Polyclinic Polyclinic, MedicalService Examination, MedicalService LabTest, Patient Patient)> SeedBasicsAsync()
    {
        await using var db = CreateContext();
        var facility = new Facility { Name = "Merkez", City = "Konya", District = "Selçuklu" };
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();
        var polyclinic = new Polyclinic { Name = "Dahiliye", FacilityId = facility.Id };
        var examination = new MedicalService { Code = "MUA", Name = "Muayene", UnitPrice = 500m };
        var lab = new MedicalService { Code = "HMG", Name = "Hemogram", UnitPrice = 120.50m };
        var patient = new Patient { FirstName = "Ayşe", LastName = "Yılmaz", NationalId = "10000000146" };
        patient.RefreshSearchName();
        db.AddRange(polyclinic, examination, lab, patient);
        await db.SaveChangesAsync();
        return (polyclinic, examination, lab, patient);
    }

    public void Dispose() => _connection.Dispose();
}
