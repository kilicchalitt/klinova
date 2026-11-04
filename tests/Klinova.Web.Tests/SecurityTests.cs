using System.Net;

namespace Klinova.Web.Tests;

public sealed class SecurityTests(KlinovaWebFactory factory) : IClassFixture<KlinovaWebFactory>
{
    [Fact]
    public async Task HealthEndpoint_IsAnonymousAndHealthy()
    {
        var response = await factory.CreateBrowser().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SiteRoot_SendsVisitorsToThePatientPortal()
    {
        var response = await factory.CreateBrowser().GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Portal", response.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("/Patients")]
    [InlineData("/Appointments")]
    [InlineData("/Admin/Users")]
    [InlineData("/Reports?handler=Csv")]
    public async Task ProtectedPages_RedirectAnonymousUsersToLogin(string path)
    {
        var response = await factory.CreateBrowser().GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        var response = await factory.CreateBrowser().GetAsync("/Account/Login");

        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Contains("script-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Login_RejectsWrongPasswordWithGenericMessage()
    {
        var client = factory.CreateBrowser();

        var response = await KlinovaWebFactory.SignInAsync(client, "kabul", "wrong-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Kullanıcı adı veya şifre hatalı.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_RejectsPostWithoutAntiforgeryToken()
    {
        var response = await factory.CreateBrowser().PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.UserName"] = "kabul",
            ["Input.Password"] = KlinovaWebFactory.DemoPassword,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SeededAdministrator_MustChangePasswordBeforeUsingTheSystem()
    {
        var client = factory.CreateBrowser();
        var login = await KlinovaWebFactory.SignInAsync(client, "admin", KlinovaWebFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var dashboard = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.Equal("/Account/ChangePassword", dashboard.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Login_IsRateLimitedPerClient()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("Klinova:LoginRequestsPerMinute", "3"));
        var client = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.GetAsync("/Account/Login")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);
    }

    [Theory]
    [InlineData("vezne", "/Admin/Users")]
    [InlineData("vezne", "/Worklist")]
    [InlineData("kabul", "/Reports")]
    [InlineData("hekim", "/Billing")]
    [InlineData("hekim", "/Patients/Create")]
    public async Task Roles_CannotOpenAreasOutsideTheirResponsibility(string user, string path)
    {
        var client = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(client, user, KlinovaWebFactory.DemoPassword);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
    }
}
