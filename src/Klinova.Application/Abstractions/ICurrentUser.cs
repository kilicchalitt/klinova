namespace Klinova.Application.Abstractions;

/// <summary>The signed-in user on whose behalf the current operation runs.</summary>
public interface ICurrentUser
{
    string? UserId { get; }

    string? UserName { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
