using Klinova.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin.Users;

public class IndexModel(UserAdministration users) : PageModel
{
    public const string TemporaryPasswordKey = "Users.TemporaryPassword";

    public List<UserListItem> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Items = await users.ListAsync(ct);
}
