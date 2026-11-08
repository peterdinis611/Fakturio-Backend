using Fakturio.Api.Validation;

namespace Fakturio.Api.Tests;

public class SlovakValidatorsTests
{
    [Theory]
    [InlineData("12345679", true)]
    [InlineData("25596641", true)]
    [InlineData("12 345 679", true)]
    [InlineData("12345678", false)]
    [InlineData("123", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    public void BeValidIco(string? ico, bool expected)
        => Assert.Equal(expected, SlovakValidators.BeValidIco(ico));

    [Theory]
    [InlineData("1234567890", true)]
    [InlineData("123", false)]
    [InlineData(null, true)]
    public void BeValidDic(string? dic, bool expected)
        => Assert.Equal(expected, SlovakValidators.BeValidDic(dic));

    [Theory]
    [InlineData("SK1234567890", true)]
    [InlineData("sk1234567890", true)]
    [InlineData("CZ1234567890", false)]
    [InlineData(null, true)]
    public void BeValidIcDph(string? icDph, bool expected)
        => Assert.Equal(expected, SlovakValidators.BeValidIcDph(icDph));

    [Theory]
    [InlineData("SK3112000000198742637541", true)]
    [InlineData("SK00 0000 0000 0000 0000 0000", false)]
    [InlineData(null, true)]
    public void BeValidIban(string? iban, bool expected)
        => Assert.Equal(expected, SlovakValidators.BeValidIban(iban));

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(19, true)]
    [InlineData(23, true)]
    [InlineData(20, false)]
    [InlineData(10, false)]
    public void BeAllowedVatRate(decimal rate, bool expected)
        => Assert.Equal(expected, SlovakValidators.BeAllowedVatRate(rate));

    [Theory]
    [InlineData("2026001", true)]
    [InlineData("12345678901", false)]
    [InlineData("ABC", false)]
    [InlineData(null, true)]
    public void BeValidVariableSymbol(string? vs, bool expected)
        => Assert.Equal(expected, SlovakValidators.BeValidVariableSymbol(vs));

    [Fact]
    public void NormalizeIco_strips_whitespace()
        => Assert.Equal("12345679", SlovakValidators.NormalizeIco("12 345 679"));
}
