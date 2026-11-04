using System.Net;

namespace Klinova.Web.Tests;

/// <summary>Every page renders for the role that is allowed to use it.</summary>
public sealed class PageSmokeTests(KlinovaWebFactory factory) : IClassFixture<KlinovaWebFactory>
{
    [Theory]
    [InlineData("kabul", "/")]
    [InlineData("kabul", "/Patients")]
    [InlineData("kabul", "/Patients?q=ayse")]
    [InlineData("kabul", "/Patients/Create")]
    [InlineData("kabul", "/Patients/Details/1")]
    [InlineData("kabul", "/Patients/Edit/1")]
    [InlineData("kabul", "/Visits")]
    [InlineData("kabul", "/Visits/Admit/1")]
    [InlineData("kabul", "/Billing")]
    [InlineData("hekim", "/Worklist")]
    [InlineData("hekim", "/Visits")]
    [InlineData("vezne", "/Billing")]
    [InlineData("vezne", "/Reports")]
    [InlineData("vezne", "/Reports?from=2026-01-01&to=2026-01-31")]
    [InlineData("vezne", "/Account/ChangePassword")]
    public async Task Page_RendersSuccessfully(string user, string path)
    {
        var client = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(client, user, KlinovaWebFactory.DemoPassword);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Klinova", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Csv_Export_ReturnsExcelFriendlyFile()
    {
        var client = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(client, "vezne", KlinovaWebFactory.DemoPassword);

        var response = await client.GetAsync("/Reports?handler=Csv&from=2026-01-01&to=2026-01-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
    }

    [Fact]
    public async Task MissingRecord_Returns404()
    {
        var client = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(client, "kabul", KlinovaWebFactory.DemoPassword);

        var response = await client.GetAsync("/Patients/Details/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
