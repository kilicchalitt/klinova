using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Common;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Web.Infrastructure;

public sealed class UserInput
{
    [Display(Name = "Kullanıcı Adı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "{0} {2}-{1} karakter olmalıdır.")]
    [RegularExpression(@"^[a-zA-Z0-9._\-@]+$", ErrorMessage = "Kullanıcı adında yalnızca İngilizce harf, rakam ve . _ - @ kullanılabilir.")]
    public string UserName { get; set; } = string.Empty;

    [Display(Name = "Ad Soyad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(100, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "E-posta")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [StringLength(120)]
    public string? Email { get; set; }

    [Display(Name = "Roller")]
    public List<string> Roles { get; set; } = [];

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public sealed record UserListItem(string Id, string UserName, string FullName, string? Email, IReadOnlyList<string> Roles, bool IsActive, bool IsLockedOut, DateTime? LastSignInAtUtc);

/// <summary>Administrator operations on user accounts built on ASP.NET Core Identity.</summary>
public sealed class UserAdministration(UserManager<AppUser> users, IClock clock, ICurrentUser currentUser)
{
    public async Task<List<UserListItem>> ListAsync(CancellationToken ct = default)
    {
        var all = await users.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct);
        var result = new List<UserListItem>(all.Count);
        foreach (var u in all)
        {
            var roles = await users.GetRolesAsync(u);
            var lockedOut = u.LockoutEnd is { } end && end > clock.UtcNow && u.IsActive;
            result.Add(new UserListItem(u.Id, u.UserName!, u.FullName, u.Email, roles.ToList(), u.IsActive, lockedOut, u.LastSignInAtUtc));
        }

        return result;
    }

    public async Task<List<LookupUser>> PhysicianAccountsAsync()
    {
        var physicians = await users.GetUsersInRoleAsync(Application.Common.Roles.Physician);
        return physicians.Where(u => u.IsActive).OrderBy(u => u.FullName).Select(u => new LookupUser(u.Id, $"{u.FullName} ({u.UserName})")).ToList();
    }

    public async Task<UserInput> GetForEditAsync(string id)
    {
        var user = await FindAsync(id);
        return new UserInput
        {
            UserName = user.UserName!,
            FullName = user.FullName,
            Email = user.Email,
            IsActive = user.IsActive,
            Roles = (await users.GetRolesAsync(user)).ToList(),
        };
    }

    /// <summary>Creates a user with a temporary password that must be changed at first sign-in.</summary>
    public async Task<(string Id, string TemporaryPassword)> CreateAsync(UserInput input)
    {
        ValidateRoles(input.Roles);
        var user = new AppUser
        {
            UserName = input.UserName.Trim(),
            FullName = input.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
        };

        var password = GeneratePassword();
        Check(await users.CreateAsync(user, password));
        Check(await users.AddToRolesAsync(user, input.Roles.Distinct()));
        return (user.Id, password);
    }

    public async Task UpdateAsync(string id, UserInput input)
    {
        ValidateRoles(input.Roles);
        var user = await FindAsync(id);
        var isSelf = user.Id == currentUser.UserId;
        var currentRoles = await users.GetRolesAsync(user);

        if (isSelf && (!input.IsActive || !input.Roles.Contains(Application.Common.Roles.Admin)))
        {
            throw new DomainException("Kendi hesabınızı pasifleştiremez veya yönetici rolünüzü kaldıramazsınız.");
        }

        if (currentRoles.Contains(Application.Common.Roles.Admin)
            && (!input.Roles.Contains(Application.Common.Roles.Admin) || !input.IsActive)
            && await ActiveAdminCountAsync() <= 1)
        {
            throw new DomainException("Sistemde en az bir aktif yönetici bulunmalıdır.");
        }

        user.UserName = input.UserName.Trim();
        user.FullName = input.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();

        var deactivated = user.IsActive && !input.IsActive;
        user.IsActive = input.IsActive;
        if (deactivated)
        {
            // Lock the account and invalidate existing sessions.
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
        }
        else if (input.IsActive && user.LockoutEnd == DateTimeOffset.MaxValue)
        {
            user.LockoutEnd = null;
        }

        Check(await users.UpdateAsync(user));
        Check(await users.RemoveFromRolesAsync(user, currentRoles.Except(input.Roles)));
        Check(await users.AddToRolesAsync(user, input.Roles.Except(currentRoles).Distinct()));
        await users.UpdateSecurityStampAsync(user);
    }

    /// <summary>Sets a new temporary password and unlocks the account.</summary>
    public async Task<string> ResetPasswordAsync(string id)
    {
        var user = await FindAsync(id);
        var password = GeneratePassword();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        Check(await users.ResetPasswordAsync(user, token, password));
        user.MustChangePassword = true;
        if (user.IsActive)
        {
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
        }

        Check(await users.UpdateAsync(user));
        return password;
    }

    private async Task<int> ActiveAdminCountAsync()
        => (await users.GetUsersInRoleAsync(Application.Common.Roles.Admin)).Count(u => u.IsActive);

    private async Task<AppUser> FindAsync(string id)
        => await users.FindByIdAsync(id) ?? throw NotFoundException.For("Kullanıcı", id);

    private static void ValidateRoles(List<string> roles)
    {
        if (roles.Count == 0)
        {
            throw new DomainException("En az bir rol seçilmelidir.");
        }

        if (roles.Any(r => !Application.Common.Roles.All.Contains(r)))
        {
            throw new DomainException("Geçersiz rol seçimi.");
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new DomainException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        Span<char> chars = stackalloc char[12];
        var all = upper + lower + digits;
        chars[0] = upper[System.Security.Cryptography.RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[System.Security.Cryptography.RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[System.Security.Cryptography.RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < chars.Length; i++)
        {
            chars[i] = all[System.Security.Cryptography.RandomNumberGenerator.GetInt32(all.Length)];
        }

        System.Security.Cryptography.RandomNumberGenerator.Shuffle(chars);
        return new string(chars);
    }
}

public sealed record LookupUser(string Id, string Name);
