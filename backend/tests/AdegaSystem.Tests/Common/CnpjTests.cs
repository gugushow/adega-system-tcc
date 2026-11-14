using AdegaSystem.Api.Common;

namespace AdegaSystem.Tests.Common;

public class CnpjTests
{
    [Theory]
    [InlineData("11.222.333/0001-81")]  // numérico, com máscara
    [InlineData("11222333000181")]      // numérico, sem máscara
    [InlineData("12.ABC.345/01DE-35")]  // alfanumérico (emitido a partir de julho de 2026)
    [InlineData("ab12cd34ef5602")]      // alfanumérico em minúsculas
    public void IsValid_AcceptsValidCnpj(string cnpj) => Assert.True(Cnpj.IsValid(cnpj));

    [Theory]
    [InlineData("11.222.333/0001-82")]  // dígito verificador errado
    [InlineData("00000000000000")]      // todos iguais
    [InlineData("1122233300018")]       // 13 caracteres
    [InlineData("12ABC34501DEAB")]      // dígitos verificadores precisam ser números
    [InlineData("")]
    public void IsValid_RejectsInvalidCnpj(string cnpj) => Assert.False(Cnpj.IsValid(cnpj));

    [Fact]
    public void Normalize_RemovesMaskAndUppercases() =>
        Assert.Equal("12ABC34501DE35", Cnpj.Normalize(" 12.abc.345/01de-35 "));
}
