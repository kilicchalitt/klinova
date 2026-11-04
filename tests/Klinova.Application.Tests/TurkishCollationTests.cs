using Klinova.Application.Common;

namespace Klinova.Application.Tests;

public class TurkishCollationTests
{
    [Fact]
    public void SortBy_UsesTurkishAlphabeticalOrder()
    {
        string[] names = ["Dahiliye", "Çocuk", "Cildiye", "Şehir", "Saray", "İç Hastalıkları", "Işık", "Göz", "Acil"];

        var sorted = names.SortBy(n => n);

        Assert.Equal(["Acil", "Cildiye", "Çocuk", "Dahiliye", "Göz", "Işık", "İç Hastalıkları", "Saray", "Şehir"], sorted);
    }
}
