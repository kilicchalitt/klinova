using System.ComponentModel.DataAnnotations;
using Klinova.Application.Abstractions;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Klinova.Web.Pages.Account;

[EnableRateLimiting("login")]
public class LoginModel(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager, IClock clock, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl());
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByNameAsync(Input.UserName.Trim());
        if (user is null || !user.IsActive)
        {
            // Same message for unknown and inactive accounts: do not reveal which user names exist.
            ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            user.LastSignInAtUtc = clock.UtcNow;
            await userManager.UpdateAsync(user);
            logger.LogInformation("User {UserName} signed in.", user.UserName);
            return LocalRedirect(SafeReturnUrl());
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("User {UserName} is locked out.", user.UserName);
            ModelState.AddModelError(string.Empty, "Çok sayıda hatalı deneme nedeniyle hesap geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
        return Page();
    }

    private string SafeReturnUrl() => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

    public sealed class LoginInput
    {
        [Display(Name = "Kullanıcı Adı")]
        [Required(ErrorMessage = "{0} zorunludur.")]
        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "Şifre")]
        [Required(ErrorMessage = "{0} zorunludur.")]
        [DataType(DataType.Password)]
        [StringLength(128)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Beni hatırla")]
        public bool RememberMe { get; set; }
    }
}
