using Klinova.Domain.Common;
using Klinova.Infrastructure.Identity;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Klinova.Web.Pages.Portal;

[EnableRateLimiting("login")]
public class RegisterModel(PortalAccounts accounts, SignInManager<AppUser> signInManager) : PageModel
{
    [BindProperty]
    public PortalRegistrationInput Input { get; set; } = new();

    public IActionResult OnGet()
        => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Portal/Index") : Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var user = await accounts.RegisterAsync(Input, ct);
            await signInManager.SignInAsync(user, isPersistent: false);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        this.Success("Hesabınız oluşturuldu. Şimdi online randevu alabilirsiniz.");
        return RedirectToPage("/Portal/Book");
    }
}
