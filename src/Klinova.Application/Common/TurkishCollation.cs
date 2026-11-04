using System.Globalization;

namespace Klinova.Application.Common;

/// <summary>
/// Turkish alphabetical ordering (Ç after C, İ after I, Ş after S…). Databases such
/// as SQLite sort by code point, so small lookup lists are ordered in memory.
/// </summary>
public static class TurkishCollation
{
    public static readonly StringComparer Comparer = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

    public static List<T> SortBy<T>(this IEnumerable<T> items, Func<T, string> key) => items.OrderBy(key, Comparer).ToList();
}
