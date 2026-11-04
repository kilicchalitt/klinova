namespace Klinova.Web.Infrastructure;

/// <summary>Redirects users flagged with "must change password" to the password change page.</summary>
public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    private static readonly PathString[] AllowedPaths =
    [
        "/Account/ChangePassword",
        "/Account/Logout",
        "/health",
        "/lib",
        "/css",
        "/js",
        "/favicon.ico",
    ];

    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.MustChangePassword()
            && !AllowedPaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Redirect("/Account/ChangePassword");
            return Task.CompletedTask;
        }

        return next(context);
    }
}
