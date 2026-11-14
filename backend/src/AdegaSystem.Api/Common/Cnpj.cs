namespace AdegaSystem.Api.Common;

/// <summary>
/// Validação de CNPJ pelos dígitos verificadores. Aceita o formato numérico tradicional
/// e o alfanumérico, emitido pela Receita Federal a partir de julho de 2026
/// (12 primeiros caracteres com letras ou números e 2 dígitos verificadores numéricos).
/// </summary>
public static class Cnpj
{
    private static readonly int[] FirstDigitWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondDigitWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Tira espaços e a máscara (". / -") e passa para maiúsculas: " 12.abc.345/01de-35" → "12ABC34501DE35".</summary>
    public static string Normalize(string cnpj) =>
        new(cnpj.Where(c => c is not ('.' or '/' or '-' or ' ')).Select(char.ToUpperInvariant).ToArray());

    public static bool IsValid(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            return false;
        }

        var value = Normalize(cnpj);
        if (value.Length != 14
            || !value[..12].All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c))
            || !value[12..].All(char.IsAsciiDigit)
            || value.All(c => c == value[0]))
        {
            return false;
        }

        var firstDigit = CalculateDigit(value[..12], FirstDigitWeights);
        var secondDigit = CalculateDigit(value[..12] + firstDigit, SecondDigitWeights);
        return value[12] - '0' == firstDigit && value[13] - '0' == secondDigit;
    }

    /// <summary>
    /// Cada caractere vale seu código ASCII menos 48: '0'..'9' valem 0..9 e 'A'..'Z' valem 17..42.
    /// Assim a mesma conta serve para o CNPJ numérico e para o alfanumérico.
    /// </summary>
    private static int CalculateDigit(string text, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            sum += (text[i] - '0') * weights[i];
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
