using Klinova.Domain.Common;
using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Users;

public class EditModel(UserAdministration users) : PageModel
{
    [BindProperty]
    public UserInput Input { get; set; } = new();

    public string? Id { get; private set; }

    public bool IsNew => Id is null;

    public async Task OnGetAsync(string? id)
    {
        Id = id;
        if (id is not null)
        {
            Input = await users.GetForEditAsync(id);
        }
    }

    public async Task<IActionResult> OnPostAsync(string? id)
    {
        Id = id;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            if (id is null)
            {
                var (_, password) = await users.CreateAsync(Input);
                TempData[IndexModel.TemporaryPasswordKey] = password;
                this.Success($"{Input.FullName} için hesap oluşturuldu.");
            }
            else
            {
                await users.UpdateAsync(id, Input);
                this.Success("Kullanıcı güncellendi.");
            }
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id)
    {
        var password = await users.ResetPasswordAsync(id);
        TempData[IndexModel.TemporaryPasswordKey] = password;
        this.Success("Şifre sıfırlandı ve hesabın kilidi açıldı.");
        return RedirectToPage("Index");
    }
}
