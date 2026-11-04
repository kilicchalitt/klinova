using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Infrastructure;

/// <summary>Flash messages shown once after a redirect (post/redirect/get).</summary>
public static class Alerts
{
    public const string SuccessKey = "Alert.Success";
    public const string ErrorKey = "Alert.Error";

    public static void Success(this PageModel page, string message) => page.TempData[SuccessKey] = message;

    public static void Error(this PageModel page, string message) => page.TempData[ErrorKey] = message;
}
