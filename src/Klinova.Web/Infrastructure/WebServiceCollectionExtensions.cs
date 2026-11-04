using System.Threading.RateLimiting;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

namespace Klinova.Web.Infrastructure;

public static class WebServiceCollectionExtensions
{
    public static IServiceCollection AddKlinovaWeb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, KlinovaClaimsPrincipalFactory>();
        services.AddScoped<UserAdministration>();

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
        });

        // Deactivated users and changed passwords take effect within minutes.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

        services.AddAuthorization(o =>
        {
            foreach (var (name, roles) in Policies.Definitions)
            {
                o.AddPolicy(name, p => p.RequireRole(roles));
            }

            o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        services.AddAntiforgery(o =>
        {
            o.Cookie.Name = "Klinova.Antiforgery";
            o.Cookie.SameSite = SameSiteMode.Strict;
        });

        services
            .AddRazorPages(o =>
            {
                o.Conventions.AllowAnonymousToPage("/Account/Login");
                o.Conventions.AllowAnonymousToPage("/Error");
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
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

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
