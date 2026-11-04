using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Klinova.Web.Tests;

/// <summary>Hosts the real application against a throw-away SQLite database.</summary>
public sealed partial class KlinovaWebFactory : WebApplicationFactory<Program>
{
    public const string AdminPassword = "Admin-Test-2026";
    public const string DemoPassword = "Demo-Test-2026";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "klinova-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_directory);
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={Path.Combine(_directory, "klinova.db")}");
        builder.UseSetting("Klinova:UseHttpsRedirection", "false");
        builder.UseSetting("Klinova:LoginRequestsPerMinute", "10000");
        builder.UseSetting("Klinova:BackgroundJobs:Enabled", "false");
        builder.UseSetting("Klinova:DataProtectionKeysPath", Path.Combine(_directory, "keys"));
        builder.UseSetting("Klinova:Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Klinova:Seed:DemoData", "true");
        builder.UseSetting("Klinova:Seed:DemoPassword", DemoPassword);
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Signs in through the login form and returns the redirect response.</summary>
    public static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string userName, string password)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.UserName"] = userName,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token,
        }));
    }

    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = TokenRegex().Match(html);
        Assert.True(match.Success, $"No antiforgery token on {path}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // SQLite may still hold the file briefly; the OS temp folder is cleaned up eventually.
        }
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
