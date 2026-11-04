using System.ComponentModel.DataAnnotations;
using Klinova.Infrastructure.Identity;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Account;

public class ChangePasswordModel(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager) : PageModel
{
    [BindProperty]
    public ChangePasswordInput Input { get; set; } = new();

    public bool IsMandatory => User.MustChangePassword();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var result = await userManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);

        this.Success("Şifreniz değiştirildi.");
        return User.IsInRole(Application.Common.Roles.Patient) ? RedirectToPage("/Portal/Index") : RedirectToPage("/Index");
    }

    public sealed class ChangePasswordInput
    {
        [Display(Name = "Mevcut Şifre")]
        [Required(ErrorMessage = "{0} zorunludur.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Display(Name = "Yeni Şifre")]
        [Required(ErrorMessage = "{0} zorunludur.")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "{0} en az {2} karakter olmalıdır.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "Yeni Şifre (Tekrar)")]
        [Required(ErrorMessage = "{0} zorunludur.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Şifreler eşleşmiyor.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
