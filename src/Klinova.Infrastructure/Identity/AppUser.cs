using Microsoft.AspNetCore.Identity;

namespace Klinova.Infrastructure.Identity;

/// <summary>A person who signs in to Klinova.</summary>
public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Inactive users cannot sign in; accounts are deactivated instead of deleted to preserve history.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set for seeded or administrator-reset accounts; the user must choose a new password at next sign-in.</summary>
    public bool MustChangePassword { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastSignInAtUtc { get; set; }
}
