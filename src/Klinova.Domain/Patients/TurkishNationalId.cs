namespace Klinova.Domain.Patients;

/// <summary>
/// Validates Turkish identity numbers (T.C. Kimlik No and foreign identity
/// numbers, which share the same checksum algorithm).
/// </summary>
/// <remarks>
/// An identity number has 11 digits, does not start with zero and carries two
/// check digits:
/// <list type="bullet">
/// <item>digit 10 = ((d1 + d3 + d5 + d7 + d9) × 7 − (d2 + d4 + d6 + d8)) mod 10</item>
/// <item>digit 11 = (d1 + d2 + … + d10) mod 10</item>
/// </list>
/// </remarks>
public static class TurkishNationalId
{
    public const int Length = 11;

    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != Length || value[0] == '0')
        {
            return false;
        }

        Span<int> digits = stackalloc int[Length];
        for (var i = 0; i < Length; i++)
        {
            var c = value[i];
            if (c is < '0' or > '9')
            {
                return false;
            }

            digits[i] = c - '0';
        }

        var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenSum = digits[1] + digits[3] + digits[5] + digits[7];

        var tenth = (((oddSum * 7) - evenSum) % 10 + 10) % 10;
        if (digits[9] != tenth)
        {
            return false;
        }

        var firstTenSum = oddSum + evenSum + digits[9];
        return digits[10] == firstTenSum % 10;
    }

    /// <summary>Returns the value with surrounding whitespace removed, or <c>null</c> when empty.</summary>
    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
