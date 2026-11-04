using System.Security.Claims;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Klinova.Web.Infrastructure;

public static class KlinovaClaimTypes
{
    public const string FullName = "klinova:full_name";
    public const string MustChangePassword = "klinova:must_change_password";
}

/// <summary>Adds the display name and the forced password change flag to the sign-in cookie.</summary>
public sealed class KlinovaClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(KlinovaClaimTypes.FullName, string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? string.Empty : user.FullName));
        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(KlinovaClaimTypes.MustChangePassword, "true"));
        }

        return identity;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static string DisplayName(this ClaimsPrincipal user)
        => user.FindFirstValue(KlinovaClaimTypes.FullName) ?? user.Identity?.Name ?? string.Empty;

    public static bool MustChangePassword(this ClaimsPrincipal user)
        => user.HasClaim(KlinovaClaimTypes.MustChangePassword, "true");

    public static bool IsInAnyRole(this ClaimsPrincipal user, params string[] roles)
        => roles.Any(user.IsInRole);
}
