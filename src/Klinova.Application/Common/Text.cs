namespace Klinova.Application.Common;

internal static class Text
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Required(string? value) => value?.Trim() ?? string.Empty;
}
