namespace AdegaSystem.Api.Common;

/// <summary>Ajustes de texto usados por vários cadastros.</summary>
public static class Text
{
    /// <summary>
    /// Campo opcional: "" ou só espaços vira null; o resto é gravado sem espaços nas pontas.
    /// Assim o banco não fica com "vazios" diferentes (null, "", "  ") para a mesma coisa.
    /// </summary>
    public static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
