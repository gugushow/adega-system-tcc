using AdegaSystem.Api.Common;

namespace AdegaSystem.Tests.Common;

public class GtinTests
{
    [Theory]
    [InlineData("036000291452", "0036000291452")]   // UPC-A → EAN-13 com 0 na frente
    [InlineData("0036000291452", "036000291452")]   // EAN-13 com 0 na frente → UPC-A
    [InlineData("012345678905", "0012345678905")]
    public void Equivalent_ConvertsBetweenUpcAAndEan13(string code, string expected) =>
        Assert.Equal(expected, Gtin.Equivalent(code));

    [Theory]
    [InlineData("036000291453")]    // dígito verificador errado
    [InlineData("7891234567895")]   // EAN-13 válido, mas não começa com 0: não tem forma UPC-A
    [InlineData("96385074")]        // EAN-8 válido: não tem equivalente aqui
    [InlineData("03600029145A")]    // letras
    [InlineData("0360002914520")]   // 13 dígitos começando com 0, mas com verificador errado
    [InlineData("")]
    [InlineData(null)]
    public void Equivalent_ReturnsNullWhenThereIsNoSafeEquivalent(string? code) =>
        Assert.Null(Gtin.Equivalent(code));

    [Theory]
    [InlineData("7891234567895")]   // EAN-13
    [InlineData("036000291452")]    // UPC-A
    [InlineData("96385074")]        // EAN-8
    public void HasValidCheckDigit_AcceptsValidCodes(string code) => Assert.True(Gtin.HasValidCheckDigit(code));

    [Theory]
    [InlineData("7891234567890")]
    [InlineData("036000291450")]
    [InlineData("1234567")]         // curto demais para um GTIN
    public void HasValidCheckDigit_RejectsInvalidCodes(string code) => Assert.False(Gtin.HasValidCheckDigit(code));
}
