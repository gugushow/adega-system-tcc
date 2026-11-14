namespace AdegaSystem.Api.Common;

/// <summary>Validação de CPF pelos dois dígitos verificadores (mesma ideia do Cnpj).</summary>
public static class Cpf
{
    /// <summary>Mantém só os dígitos: "123.456.789-09" → "12345678909".</summary>
    public static string Normalize(string cpf) => new(cpf.Where(char.IsAsciiDigit).ToArray());

    public static bool IsValid(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return false;
        }

        // Só aceita dígitos e a máscara comum. "123abc" não vira "123" escondido.
        if (cpf.Any(c => !char.IsAsciiDigit(c) && c is not ('.' or '-' or ' ')))
        {
            return false;
        }

        var value = Normalize(cpf);

        // 111.111.111-11 passa na conta dos dígitos, mas não é um CPF de verdade.
        if (value.Length != 11 || value.All(c => c == value[0]))
        {
            return false;
        }

        return value[9] - '0' == CalculateDigit(value[..9]) && value[10] - '0' == CalculateDigit(value[..10]);
    }

    /// <summary>
    /// Pesos decrescentes (10, 9, ..., 2 para o 1º dígito; 11, 10, ..., 2 para o 2º).
    /// Resto da soma por 11: menor que 2 vira 0, senão 11 − resto.
    /// </summary>
    private static int CalculateDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            sum += (digits[i] - '0') * (digits.Length + 1 - i);
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
