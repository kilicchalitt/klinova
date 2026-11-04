using System.Globalization;
using Klinova.Application;
using Klinova.Infrastructure;
using Klinova.Infrastructure.Persistence.Seeding;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKlinovaApplication();
builder.Services.AddKlinovaInfrastructure(builder.Configuration);
builder.Services.AddKlinovaWeb(builder.Configuration);

var app = builder.Build();

var turkish = new CultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkish),
    SupportedCultures = [turkish],
    SupportedUICultures = [turkish],
});

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}");
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Configuration.GetValue("Klinova:UseHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PasswordChangeRequiredMiddleware>();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapRazorPages();

await DatabaseInitializer.InitializeAsync(app.Services);
await app.RunAsync();

/// <summary>Entry point; public so that integration tests can use WebApplicationFactory.</summary>
public partial class Program
{
    protected Program()
    {
    }
}
