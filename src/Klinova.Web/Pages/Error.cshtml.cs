using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public int StatusCodeValue { get; private set; } = 500;

    public string? RequestId { get; private set; }

    public void OnGet(int? statusCode) => Populate(statusCode);

    public void OnPost(int? statusCode) => Populate(statusCode);

    private void Populate(int? statusCode)
    {
        StatusCodeValue = statusCode ?? 500;
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
