using Ngangi.Core.Services;
using Xunit;

namespace Ngangi.Tests;

public class FormattingTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1000, "1 000")]
    [InlineData(1240000, "1 240 000")]
    [InlineData(-2500, "-2 500")]
    public void Number_Uses_French_Thousands_Separator(long value, string expected)
        => Assert.Equal(expected, Formatting.Number(value));

    [Theory]
    [InlineData(0, "0 FCFA")]
    [InlineData(340000, "340 000 FCFA")]
    [InlineData(1240000, "1 240 000 FCFA")]
    public void Fcfa_Matches_Exact_Core_Amount(long value, string expected)
        => Assert.Equal(expected, Formatting.Fcfa(value));
}
