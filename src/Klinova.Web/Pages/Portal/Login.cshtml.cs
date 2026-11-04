using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Klinova.Web.Pages.Portal;

[EnableRateLimiting("login")]
public class LoginModel(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager, IClock clock) : PageModel
{
    [BindProperty]
    public PortalLoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
        => User.IsInRole(Roles.Patient) ? RedirectToPage("/Portal/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByNameAsync(Input.NationalId.Trim());
        if (user is null || !user.IsActive || !await userManager.IsInRoleAsync(user, Roles.Patient))
        {
            ModelState.AddModelError(string.Empty, "T.C. Kimlik No veya şifre hatalı.");
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Çok sayıda hatalı deneme nedeniyle hesabınız 15 dakika kilitlendi.");
            return Page();
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "T.C. Kimlik No veya şifre hatalı.");
            return Page();
        }

        user.LastSignInAtUtc = clock.UtcNow;
        await userManager.UpdateAsync(user);
        return !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) && ReturnUrl.StartsWith("/Portal", StringComparison.OrdinalIgnoreCase)
            ? LocalRedirect(ReturnUrl)
            : RedirectToPage("/Portal/Index");
    }

    public sealed class PortalLoginInput
    {
        [Display(Name = "T.C. Kimlik No")]
        [Required(ErrorMessage = "{0} zorunludur.")]
        [StringLength(11, MinimumLength = 11, ErrorMessage = "{0} 11 haneli olmalıdır.")]
        public string NationalId { get; set; } = string.Empty;

        [Display(Name = "Şifre")]
        [Required(ErrorMessage = "{0} zorunludur.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Beni hatırla")]
        public bool RememberMe { get; set; }
    }
}
