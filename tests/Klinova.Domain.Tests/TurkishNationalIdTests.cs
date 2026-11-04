using Klinova.Domain.Patients;

namespace Klinova.Domain.Tests;

public class TurkishNationalIdTests
{
    [Theory]
    [InlineData("10000000146")]
    [InlineData("11111111110")]
    [InlineData("22222222220")]
    [InlineData("12345678950")]
    [InlineData("99999999990")]
    public void IsValid_ReturnsTrue_ForNumbersWithCorrectCheckDigits(string value)
        => Assert.True(TurkishNationalId.IsValid(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890")]      // 10 digits
    [InlineData("123456789012")]    // 12 digits
    [InlineData("01234567890")]     // leading zero
    [InlineData("1234567895A")]     // non-digit
    [InlineData("12345678951")]     // wrong 11th digit
    [InlineData("12345678940")]     // wrong 10th digit
    [InlineData("11111111111")]
    public void IsValid_ReturnsFalse_ForInvalidNumbers(string? value)
        => Assert.False(TurkishNationalId.IsValid(value));

    [Theory]
    [InlineData("  10000000146 ", "10000000146")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalize_TrimsAndTurnsBlankIntoNull(string? input, string? expected)
        => Assert.Equal(expected, TurkishNationalId.Normalize(input));
}
