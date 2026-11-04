using Klinova.Domain.Patients;

namespace Klinova.Domain.Tests;

public class PatientTests
{
    [Theory]
    [InlineData("2000-06-15", "2026-06-14", 25)]
    [InlineData("2000-06-15", "2026-06-15", 26)]
    [InlineData("2024-02-29", "2025-02-28", 0)]
    public void AgeOn_AccountsForBirthdayNotYetReached(string birth, string on, int expected)
    {
        var patient = new Patient { BirthDate = DateOnly.Parse(birth, System.Globalization.CultureInfo.InvariantCulture) };

        Assert.Equal(expected, patient.AgeOn(DateOnly.Parse(on, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void AgeOn_ReturnsNull_WhenBirthDateUnknown()
        => Assert.Null(new Patient().AgeOn(new DateOnly(2026, 1, 1)));

    [Theory]
    [InlineData("42", 42)]
    [InlineData("00000042", 42)]
    [InlineData(" 7 ", 7)]
    public void TryParseFileNumber_AcceptsPaddedAndUnpaddedNumbers(string input, int expected)
    {
        Assert.True(Patient.TryParseFileNumber(input, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("12345678901")]
    public void TryParseFileNumber_RejectsInvalidInput(string? input)
        => Assert.False(Patient.TryParseFileNumber(input, out _));

    [Fact]
    public void FormatFileNumber_PadsToEightDigits()
        => Assert.Equal("00000042", Patient.FormatFileNumber(42));
}
