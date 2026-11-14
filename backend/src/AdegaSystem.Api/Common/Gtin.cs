namespace AdegaSystem.Api.Common;

/// <summary>
/// Equivalência entre UPC-A (12 dígitos) e EAN-13 começando com 0. São o mesmo GTIN escrito de
/// dois jeitos: o EAN-13 só acrescenta um 0 na frente, e o dígito verificador é o mesmo nos dois.
/// Alguns leitores (e câmeras) devolvem "012345678905" e outros "0012345678905" para a mesma
/// embalagem. Os códigos salvos no banco nunca são reescritos; isto serve só para a busca e para
/// impedir que dois produtos diferentes fiquem com o mesmo GTIN escrito de formas diferentes.
/// </summary>
public static class Gtin
{
    /// <summary>
    /// A outra forma do mesmo código, ou null quando não existe equivalência segura:
    /// "036000291452" (UPC-A) → "0036000291452" e o inverso. Só vale para códigos só com dígitos
    /// e com dígito verificador correto, para nunca "achar" um produto por um código digitado errado.
    /// </summary>
    public static string? Equivalent(string? code)
    {
        if (code is null || !code.All(char.IsAsciiDigit) || !HasValidCheckDigit(code))
        {
            return null;
        }

        return code.Length switch
        {
            12 => "0" + code,
            13 when code[0] == '0' => code[1..],
            _ => null,
        };
    }

    /// <summary>
    /// Dígito verificador GTIN (EAN-8, UPC-A, EAN-13): da direita para a esquerda, sem contar o
    /// próprio verificador, os dígitos alternam peso 3 e 1. O verificador completa a soma até a
    /// próxima dezena. Um 0 à esquerda não muda a soma, por isso UPC-A e EAN-13 batem.
    /// </summary>
    public static bool HasValidCheckDigit(string digits)
    {
        if (digits.Length < 8 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = digits.Length - 2; i >= 0; i--)
        {
            var positionFromRight = digits.Length - 1 - i;
            sum += (digits[i] - '0') * (positionFromRight % 2 == 1 ? 3 : 1);
        }

        var expected = (10 - sum % 10) % 10;
        return digits[^1] - '0' == expected;
    }
}
