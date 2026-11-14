using System.Globalization;

namespace AdegaSystem.Api.Common;

/// <summary>Valores em reais nas mensagens de erro: 1234.5 → "R$ 1.234,50".</summary>
public static class Money
{
    // Formato montado à mão para não depender da cultura instalada no servidor.
    private static readonly NumberFormatInfo Brazilian = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    public static string Format(decimal value) => "R$ " + value.ToString("N2", Brazilian);
}
