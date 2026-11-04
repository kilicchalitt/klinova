using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.WebEncoders;

namespace Klinova.Web.Infrastructure;

public static class WebServiceCollectionExtensions
{
    public static IServiceCollection AddKlinovaWeb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, KlinovaClaimsPrincipalFactory>();
        services.AddScoped<UserAdministration>();
        services.AddScoped<PortalAccounts>();

        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "Klinova.Auth";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.LoginPath = "/Account/Login";
            o.LogoutPath = "/Account/Logout";
            o.AccessDeniedPath = "/Account/AccessDenied";
            o.ExpireTimeSpan = TimeSpan.FromHours(10);
            o.SlidingExpiration = true;

            // Patients sign in and land in the portal, staff in the staff application.
            o.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/Portal", StringComparison.OrdinalIgnoreCase))
                {
                    var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                    context.Response.Redirect("/Portal/Login?ReturnUrl=" + Uri.EscapeDataString(returnUrl));
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }

                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.Redirect(context.HttpContext.User.IsInRole(Application.Common.Roles.Patient) ? "/Portal" : context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        // Deactivated users and changed passwords take effect within minutes.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

        services.AddAuthorization(o =>
        {
            foreach (var (name, roles) in Policies.Definitions)
            {
                o.AddPolicy(name, p => p.RequireRole(roles));
            }

            // Everything not explicitly opened up is reserved for staff.
            o.FallbackPolicy = o.GetPolicy(Policies.Staff);
        });

        services.AddAntiforgery(o =>
        {
            o.Cookie.Name = "Klinova.Antiforgery";
            o.Cookie.SameSite = SameSiteMode.Strict;
        });

        services
            .AddRazorPages(o =>
            {
                o.Conventions.AllowAnonymousToPage("/Index");
                o.Conventions.AllowAnonymousToPage("/Account/Login");
                o.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
                o.Conventions.AllowAnonymousToPage("/Error");
                o.Conventions.AuthorizePage("/Account/Logout");
                o.Conventions.AuthorizePage("/Account/ChangePassword");

                o.Conventions.AuthorizeFolder("/Portal", Policies.Patient);
                o.Conventions.AllowAnonymousToPage("/Portal/Index");
                o.Conventions.AllowAnonymousToPage("/Portal/Login");
                o.Conventions.AllowAnonymousToPage("/Portal/Register");
                o.Conventions.AllowAnonymousToPage("/Portal/Privacy");
                o.Conventions.AllowAnonymousToPage("/Portal/Book"); // anyone may browse free slots; booking requires sign-in

                o.Conventions.AuthorizeFolder("/Appointments", Policies.AppointmentView);
                o.Conventions.AuthorizePage("/Appointments/Book", Policies.AppointmentDesk);
                o.Conventions.AuthorizePage("/Appointments/Waitlist", Policies.AppointmentDesk);
                o.Conventions.AuthorizeFolder("/Admin", Policies.Administration);
                o.Conventions.AuthorizeFolder("/Reports", Policies.Reporting);
                o.Conventions.AuthorizeFolder("/Billing", Policies.Billing);
                o.Conventions.AuthorizeFolder("/Worklist", Policies.ClinicalWork);
                o.Conventions.AuthorizePage("/Patients/Create", Policies.PatientRegistration);
                o.Conventions.AuthorizePage("/Patients/Edit", Policies.PatientRegistration);
                o.Conventions.AuthorizePage("/Visits/Admit", Policies.PatientRegistration);
            })
            .AddMvcOptions(o =>
            {
                o.ModelBinderProviders.Insert(0, new FlexibleDecimalModelBinderProvider());
                o.Filters.Add<ExceptionPageFilter>();
            });

        // Throttle sign-in attempts per client address in addition to Identity's account lockout.
        var loginLimit = configuration.GetValue("Klinova:LoginRequestsPerMinute", 20);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        // Emit Turkish characters as-is instead of HTML entities (smaller, readable markup).
        services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Trust the proxies listed in configuration (e.g. "Klinova:KnownProxies": ["10.0.0.5"]).
            foreach (var proxy in configuration.GetSection("Klinova:KnownProxies").Get<string[]>() ?? [])
            {
                if (System.Net.IPAddress.TryParse(proxy, out var address))
                {
                    o.KnownProxies.Add(address);
                }
            }
        });

        services.AddHsts(o =>
        {
            o.MaxAge = TimeSpan.FromDays(365);
            o.IncludeSubDomains = true;
        });

        return services;
    }
}
