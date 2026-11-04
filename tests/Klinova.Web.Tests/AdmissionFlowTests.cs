using System.Net;

namespace Klinova.Web.Tests;

public sealed class AdmissionFlowTests(KlinovaWebFactory factory) : IClassFixture<KlinovaWebFactory>
{
    [Fact]
    public async Task Reception_CanRegisterAndAdmitAPatient()
    {
        var client = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(client, "kabul", KlinovaWebFactory.DemoPassword);

        var token = await KlinovaWebFactory.GetAntiforgeryTokenAsync(client, "/Patients/Create");
        var register = await client.PostFormAsync("/Patients/Create", new Dictionary<string, string>
        {
            ["Input.FirstName"] = "Entegrasyon",
            ["Input.LastName"] = "Testi",
            ["Input.NationalId"] = "10000000078",
            ["Input.BirthDate"] = "1990-05-17",
            ["AdmitAfterSave"] = "true",
            ["__RequestVerificationToken"] = token,
        });

        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);
        var admitPath = register.Headers.Location!.OriginalString;
        Assert.StartsWith("/Visits/Admit/", admitPath, StringComparison.Ordinal);

        token = await KlinovaWebFactory.GetAntiforgeryTokenAsync(client, admitPath);
        var admit = await client.PostFormAsync(admitPath, new Dictionary<string, string>
        {
            ["Input.PolyclinicId"] = "1",
            ["Input.Complaint"] = "Kontrol",
            ["printSlip"] = "false",
            ["__RequestVerificationToken"] = token,
        });

        Assert.Equal(HttpStatusCode.Redirect, admit.StatusCode);
        Assert.StartsWith("/Visits/Details/", admit.Headers.Location!.OriginalString, StringComparison.Ordinal);

        var details = await client.GetAsync(admit.Headers.Location);
        var html = await details.Content.ReadAsStringAsync();
        Assert.Contains("Entegrasyon Testi", html, StringComparison.Ordinal);
        Assert.Contains("Sırada", html, StringComparison.Ordinal);
    }
}
