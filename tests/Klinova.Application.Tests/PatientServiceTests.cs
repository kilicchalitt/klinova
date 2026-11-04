using Klinova.Application.Patients;
using Klinova.Domain.Common;

namespace Klinova.Application.Tests;

public sealed class PatientServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static PatientInput Input(string first, string last, string? nationalId = null)
        => new() { FirstName = first, LastName = last, NationalId = nationalId };

    [Fact]
    public async Task RegisterAsync_PersistsPatientAndAuditStamp()
    {
        await using var ctx = _db.CreateContext();
        var service = new PatientService(ctx);

        var id = await service.RegisterAsync(Input("  Mehmet ", "Öztürk", "10000000146"));

        var patient = await service.GetAsync(id);
        Assert.Equal("Mehmet", patient.FirstName);
        Assert.Equal("MEHMET OZTURK", patient.SearchName);
        Assert.Equal("tester", patient.CreatedBy);
        Assert.Equal(_db.Clock.UtcNow, patient.CreatedAtUtc);
    }

    [Fact]
    public async Task RegisterAsync_RejectsDuplicateNationalId()
    {
        await using var ctx = _db.CreateContext();
        var service = new PatientService(ctx);
        await service.RegisterAsync(Input("Ali", "Veli", "10000000146"));

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.RegisterAsync(Input("Başka", "Kişi", "10000000146")));
        Assert.Contains("Ali Veli", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_AllowsKeepingOwnNationalId()
    {
        await using var ctx = _db.CreateContext();
        var service = new PatientService(ctx);
        var id = await service.RegisterAsync(Input("Ali", "Veli", "10000000146"));

        await service.UpdateAsync(id, Input("Ali Can", "Veli", "10000000146"));

        Assert.Equal("Ali Can", (await service.GetAsync(id)).FirstName);
    }

    [Theory]
    [InlineData("ismail")]
    [InlineData("İSMAİL")]
    [InlineData("Ismail kar")]
    public async Task SearchAsync_FindsNamesRegardlessOfTurkishCasing(string term)
    {
        await using var ctx = _db.CreateContext();
        var service = new PatientService(ctx);
        await service.RegisterAsync(Input("İsmail", "Karaca"));
        await service.RegisterAsync(Input("Fatma", "Şen"));

        var result = await service.SearchAsync(new PatientSearchQuery(term));

        var match = Assert.Single(result.Items);
        Assert.Equal("İsmail Karaca", match.FullName);
    }

    [Fact]
    public async Task SearchAsync_FindsByFileNumberAndNationalId()
    {
        await using var ctx = _db.CreateContext();
        var service = new PatientService(ctx);
        var first = await service.RegisterAsync(Input("Ali", "Veli", "10000000146"));
        await service.RegisterAsync(Input("Can", "Demir"));

        Assert.Equal(first, Assert.Single((await service.SearchAsync(new PatientSearchQuery("10000000146"))).Items).Id);
        Assert.Equal(first, Assert.Single((await service.SearchAsync(new PatientSearchQuery(first.ToString("D8", System.Globalization.CultureInfo.InvariantCulture)))).Items).Id);
    }

    [Fact]
    public async Task SearchAsync_ExcludesArchivedPatientsByDefault()
    {
        await using var ctx = _db.CreateContext();
        var service = new PatientService(ctx);
        var id = await service.RegisterAsync(Input("Ali", "Veli"));
        await service.SetArchivedAsync(id, archived: true);

        Assert.Empty((await service.SearchAsync(new PatientSearchQuery())).Items);
        Assert.Single((await service.SearchAsync(new PatientSearchQuery(IncludeArchived: true))).Items);
    }

    [Fact]
    public async Task SearchAsync_PagesResults()
    {
        await using var ctx = _db.CreateContext();
        var service = new PatientService(ctx);
        for (var i = 0; i < 25; i++)
        {
            await service.RegisterAsync(Input($"Hasta{i:D2}", "Test"));
        }

        var page = await service.SearchAsync(new PatientSearchQuery(Page: 3, PageSize: 10));

        Assert.Equal(25, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(5, page.Items.Count);
        Assert.False(page.HasNext);
    }
}
