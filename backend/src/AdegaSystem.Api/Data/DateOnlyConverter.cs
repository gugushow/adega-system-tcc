using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdegaSystem.Api.Data;

/// <summary>
/// O provider oficial do MySQL (MySql.EntityFrameworkCore 10) grava DateOnly, mas falha ao ler
/// de volta ("Unable to cast DateTime to DateOnly"). Este conversor entrega e recebe DateTime
/// do provider, e a coluna continua sendo do tipo date (ver AdegaDbContext.ConfigureConventions).
/// </summary>
public class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
    toDatabase => toDatabase.ToDateTime(TimeOnly.MinValue),
    fromDatabase => DateOnly.FromDateTime(fromDatabase));
