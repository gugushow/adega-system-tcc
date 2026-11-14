namespace AdegaSystem.Api.Common;

/// <summary>
/// O banco guarda as datas em UTC, mas "hoje" e "o dia 01/10" são no horário de Brasília.
/// Às 22h de 01/10 em Brasília já é 01h de 02/10 em UTC, então filtrar por dia sem
/// converter faria movimentações da noite aparecerem no dia seguinte.
/// </summary>
public static class BusinessClock
{
    // Id IANA: funciona no Linux e no Windows (.NET 6+).
    private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Data de hoje no horário de Brasília.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone));

    /// <summary>Instante UTC em que o dia começa em Brasília. Ex.: 01/10 → 01/10 03:00 UTC.</summary>
    public static DateTime StartOfDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), TimeZone);

    /// <summary>Em que dia (no horário de Brasília) aconteceu um instante UTC. Ex.: 02/10 01:30 UTC → 01/10.</summary>
    public static DateOnly ToBusinessDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone));

    /// <summary>Primeiro dia do mês da data. Ex.: 17/10/2026 → 01/10/2026.</summary>
    public static DateOnly FirstDayOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}
