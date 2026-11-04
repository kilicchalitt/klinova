using Klinova.Domain.Common;
using Klinova.Domain.Patients;

namespace Klinova.Domain.Tests;

public class SearchNormalizerTests
{
    [Theory]
    [InlineData("ismail", "ISMAIL")]
    [InlineData("İSMAİL", "ISMAIL")]
    [InlineData("Ismail", "ISMAIL")]
    [InlineData("  Şükrü   Öğüç  ", "SUKRU OGUC")]
    [InlineData("ışık", "ISIK")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalize_FoldsTurkishCharactersAndWhitespace(string? input, string expected)
        => Assert.Equal(expected, SearchNormalizer.Normalize(input));

    [Fact]
    public void Patient_RefreshSearchName_UsesFullName()
    {
        var patient = new Patient { FirstName = "Gül", LastName = "Çelik" };

        patient.RefreshSearchName();

        Assert.Equal("GUL CELIK", patient.SearchName);
    }
}
