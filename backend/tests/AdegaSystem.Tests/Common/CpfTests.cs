using AdegaSystem.Api.Common;

namespace AdegaSystem.Tests.Common;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]  // com máscara
    [InlineData("52998224725")]     // sem máscara
    [InlineData("123.456.789-09")]  // outro CPF válido, com máscara
    public void IsValid_AcceptsValidCpf(string cpf) => Assert.True(Cpf.IsValid(cpf));

    [Theory]
    [InlineData("529.982.247-26")]  // dígito verificador errado
    [InlineData("111.111.111-11")]  // todos iguais (a conta fecha, mas não é CPF de verdade)
    [InlineData("5299822472")]      // 10 dígitos
    [InlineData("529982247250")]    // 12 dígitos
    [InlineData("529.982.247-2A")]  // letras não são aceitas
    [InlineData("")]
    public void IsValid_RejectsInvalidCpf(string cpf) => Assert.False(Cpf.IsValid(cpf));

    [Fact]
    public void Normalize_RemovesMask() =>
        Assert.Equal("52998224725", Cpf.Normalize(" 529.982.247-25 "));
}
