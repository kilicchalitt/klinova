using System.Globalization;
using System.Text;

namespace Klinova.Domain.Common;

/// <summary>
/// Produces an accent- and case-insensitive representation of Turkish text so that
/// "ismail", "İSMAİL" and "Ismail" all match the same record on every database provider.
/// </summary>
public static class SearchNormalizer
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var upper = value.Trim().ToUpper(Turkish);
        var builder = new StringBuilder(upper.Length);
        var previousWasSpace = false;

        foreach (var c in upper)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            builder.Append(c switch
            {
                'İ' => 'I',
                'Ş' => 'S',
                'Ğ' => 'G',
                'Ü' => 'U',
                'Ö' => 'O',
                'Ç' => 'C',
                'Â' => 'A',
                'Î' => 'I',
                'Û' => 'U',
                _ => c,
            });
        }

        return builder.ToString();
    }
}
