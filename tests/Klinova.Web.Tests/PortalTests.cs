using System.Net;
using System.Text.RegularExpressions;

namespace Klinova.Web.Tests;

public sealed partial class PortalTests(KlinovaWebFactory factory) : IClassFixture<KlinovaWebFactory>
{
    private const string Password = "Hasta-Test-2026";

    /// <summary>Builds a T.C. Kimlik No with valid check digits from nine digits.</summary>
    private static string NationalId(int seed)
    {
        var d = (100000000 + seed).ToString(System.Globalization.CultureInfo.InvariantCulture).Select(c => c - '0').ToList();
        var tenth = ((((d[0] + d[2] + d[4] + d[6] + d[8]) * 7) - (d[1] + d[3] + d[5] + d[7])) % 10 + 10) % 10;
        d.Add(tenth);
        d.Add(d.Sum() % 10);
        return string.Concat(d);
    }

    private static Dictionary<string, string> Registration(string nationalId, string first, string last, string phone) => new()
    {
        ["Input.NationalId"] = nationalId,
        ["Input.FirstName"] = first,
        ["Input.LastName"] = last,
        ["Input.BirthDate"] = "1990-05-17",
        ["Input.Phone"] = phone,
        ["Input.Password"] = Password,
        ["Input.ConfirmPassword"] = Password,
        ["Input.AcceptPrivacyNotice"] = "true",
    };

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string pagePath, string postPath, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = await KlinovaWebFactory.GetAntiforgeryTokenAsync(client, pagePath);
        return await client.PostAsync(postPath, new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task Portal_IsPublicAndListsFacilities()
    {
        var response = await factory.CreateBrowser().GetAsync("/Portal");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Online randevu", html, StringComparison.Ordinal);
        Assert.Contains("Semt Polikliniği", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FreeSlots_CanBeBrowsedWithoutSigningIn_ButBookingRequiresSignIn()
    {
        var client = factory.CreateBrowser();
        var page = await client.GetAsync("/Portal/Book?polyclinicId=1");
        var html = await page.Content.ReadAsStringAsync();
        var slot = SlotRegex().Match(html);
        Assert.True(slot.Success, "No free slot rendered");

        var post = await PostFormAsync(client, "/Portal/Book?polyclinicId=1", "/Portal/Book?handler=Book&polyclinicId=1", new() { ["slot"] = slot.Groups[1].Value });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.StartsWith("/Portal/Login", post.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewPatient_RegistersBooksAndSeesTheAppointment()
    {
        var client = factory.CreateBrowser();
        var register = await PostFormAsync(client, "/Portal/Register", "/Portal/Register", Registration(NationalId(11), "Portal", "Hastası", "0532 000 11 11"));
        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);
        Assert.Equal("/Portal/Book", register.Headers.Location!.OriginalString);

        var bookPage = await (await client.GetAsync("/Portal/Book?polyclinicId=1")).Content.ReadAsStringAsync();
        var slot = SlotRegex().Match(bookPage).Groups[1].Value;
        var book = await PostFormAsync(client, "/Portal/Book?polyclinicId=1", "/Portal/Book?handler=Book&polyclinicId=1", new() { ["slot"] = slot, ["note"] = "Kontrol" });
        Assert.Equal(HttpStatusCode.Redirect, book.StatusCode);
        Assert.Equal("/Portal/Appointments", book.Headers.Location!.OriginalString);

        var appointments = await (await client.GetAsync("/Portal/Appointments")).Content.ReadAsStringAsync();
        Assert.Contains("Randevunuz oluşturuldu", appointments, StringComparison.Ordinal);
        Assert.Contains("Dahiliye", appointments, StringComparison.Ordinal);
        Assert.Contains("Kontrol", appointments, StringComparison.Ordinal);

        // Without an SMS provider the confirmation is delivered to the portal inbox.
        var home = await (await client.GetAsync("/Portal")).Content.ReadAsStringAsync();
        Assert.Contains("Yeni bildirimleriniz", home, StringComparison.Ordinal);
        var inbox = await (await client.GetAsync("/Portal/Notifications")).Content.ReadAsStringAsync();
        Assert.Contains("Randevu onayı", inbox, StringComparison.Ordinal);
        Assert.Contains("Randevu kodu", inbox, StringComparison.Ordinal);
        var afterReading = await (await client.GetAsync("/Portal")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Yeni bildirimleriniz", afterReading, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingPatient_IsLinkedOnlyWhenThePhoneOnFileMatches()
    {
        var nationalId = NationalId(22);
        var staff = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(staff, "kabul", KlinovaWebFactory.DemoPassword);
        var created = await PostFormAsync(staff, "/Patients/Create", "/Patients/Create", new()
        {
            ["Input.FirstName"] = "Kayıtlı",
            ["Input.LastName"] = "Hasta",
            ["Input.NationalId"] = nationalId,
            ["Input.BirthDate"] = "1990-05-17",
            ["Input.Phone"] = "0532 222 22 22",
            ["AdmitAfterSave"] = "false",
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        var attacker = factory.CreateBrowser();
        var rejected = await PostFormAsync(attacker, "/Portal/Register", "/Portal/Register", Registration(nationalId, "Kayıtlı", "Hasta", "0555 999 99 99"));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("kurumdaki kaydınızla eşleşmiyor", await rejected.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var owner = factory.CreateBrowser();
        var accepted = await PostFormAsync(owner, "/Portal/Register", "/Portal/Register", Registration(nationalId, "KAYITLI", "hasta", "+90 532 222 22 22"));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);

        var profile = await (await owner.GetAsync("/Portal/Profile")).Content.ReadAsStringAsync();
        Assert.Contains("Kayıtlı Hasta", profile, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registration_RequiresPrivacyNoticeAndValidNationalId()
    {
        var client = factory.CreateBrowser();
        var form = Registration("12345678901", "Ad", "Soyad", "0532 333 33 33");
        form.Remove("Input.AcceptPrivacyNotice");

        var response = await PostFormAsync(client, "/Portal/Register", "/Portal/Register", form);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Geçerli bir T.C. Kimlik No giriniz.", html, StringComparison.Ordinal);
        Assert.Contains("aydınlatma metnini kabul etmelisiniz", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Patients")]
    [InlineData("/Appointments")]
    [InlineData("/Admin/Users")]
    [InlineData("/")]
    public async Task Patients_AreKeptOutOfTheStaffApplication(string path)
    {
        var client = factory.CreateBrowser();
        await PostFormAsync(client, "/Portal/Register", "/Portal/Register", Registration(NationalId(33 + path.Length), "Meraklı", "Hasta", "0532 444 44 44"));

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Portal", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Staff_CannotUseThePatientPortalPages()
    {
        var client = factory.CreateBrowser();
        await KlinovaWebFactory.SignInAsync(client, "kabul", KlinovaWebFactory.DemoPassword);

        var response = await client.GetAsync("/Portal/Appointments");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
    }

    [GeneratedRegex("name=\"slot\" id=\"[^\"]+\" value=\"([^\"]+)\"")]
    private static partial Regex SlotRegex();
}
