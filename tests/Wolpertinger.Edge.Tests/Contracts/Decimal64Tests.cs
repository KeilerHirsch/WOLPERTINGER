using Wolpertinger.Edge.Contracts;

namespace Wolpertinger.Edge.Tests.Contracts;

public sealed class Decimal64Tests
{
    [Theory]
    [InlineData("36.0340", 36034L, -3)]
    [InlineData("3.6034E1", 36034L, -3)]
    [InlineData("0.000000", 0L, 0)]
    public void ParseCanonicalizesEquivalentLexemes(string text, long coefficient, sbyte exponent)
    {
        var value = Decimal64.Parse(text);

        Assert.Equal(new Decimal64(coefficient, exponent), value);
    }

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    public void ParseRejectsCoefficientOverflow(string text)
        => Assert.Throws<OverflowException>(() => Decimal64.Parse(text));

    [Theory]
    [InlineData("1e19")]
    [InlineData("1e-19")]
    public void ParseRejectsExponentOutsideTrustedRange(string text)
        => Assert.Throws<OverflowException>(() => Decimal64.Parse(text));
    [Theory]
    [InlineData(36034L, -3, "36.034")]
    [InlineData(1L, -3, "0.001")]
    [InlineData(-5L, 2, "-500")]
    [InlineData(0L, 0, "0")]
    public void ToStringFormatsCanonicalExactDecimal(long coefficient, sbyte exponent, string expected)
        => Assert.Equal(expected, new Decimal64(coefficient, exponent).ToString());
}