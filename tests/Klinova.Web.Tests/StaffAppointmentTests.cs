using System.Net;

namespace Klinova.Web.Tests;

public sealed class StaffAppointmentTests(KlinovaWebFactory factory) : IClassFixture<KlinovaWebFactory>
{
    [Theory]
    [InlineData("kabul", "/Appointments")]
    [InlineData("kabul", "/Appointments?date=2026-11-05&status=0")]
    [InlineData("kabul", "/Appointments/Book?patientId=1")]
    [InlineData("kabul", "/Appointments/Book?patientId=1&polyclinicId=1")]
    [InlineData("kabul", "/Appointments/Waitlist")]
    [InlineData("hekim", "/Appointments")]
    public async Task AppointmentPages_Render(string user, string path)
    {
        var client = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(client, user, KlinovaWebFactory.DemoPassword);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("vezne", "/Appointments")]
    [InlineData("hekim", "/Appointments/Book?patientId=1")]
    [InlineData("hekim", "/Appointments/Waitlist")]
    public async Task AppointmentPages_AreRestrictedByRole(string user, string path)
    {
        var client = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(client, user, KlinovaWebFactory.DemoPassword);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrator_CanOpenEveryAdministrationPage()
    {
        var client = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(client, "admin", KlinovaWebFactory.AdminPassword);
        var token = await KlinovaWebFactory.GetAntiforgeryTokenAsync(client, "/Account/ChangePassword");
        var changed = await client.PostFormAsync("/Account/ChangePassword", new Dictionary<string, string>
        {
            ["Input.CurrentPassword"] = KlinovaWebFactory.AdminPassword,
            ["Input.NewPassword"] = "Admin-Yeni-2026",
            ["Input.ConfirmPassword"] = "Admin-Yeni-2026",
            ["__RequestVerificationToken"] = token,
        });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);

        string[] pages =
        [
            "/", "/Admin/Facilities", "/Admin/Facilities/Edit", "/Admin/Facilities/Edit/1", "/Admin/Polyclinics", "/Admin/Polyclinics/Edit/1",
            "/Admin/Doctors", "/Admin/Schedules", "/Admin/Schedules?doctorId=1", "/Admin/Holidays", "/Admin/Holidays?year=2027",
            "/Admin/Notifications", "/Admin/Notifications?status=Sent", "/Admin/Users", "/Admin/Settings", "/Admin/AuditLogs", "/Patients/Details/1",
        ];
        foreach (var page in pages)
        {
            var response = await client.GetAsync(page);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{page} returned {(int)response.StatusCode}");
        }
    }
}
