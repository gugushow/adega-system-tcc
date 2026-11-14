using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdegaSystem.Api.Data;

/// <summary>
/// Gravamos todas as datas em UTC, mas a coluna datetime do MySQL não guarda o fuso.
/// Sem este conversor, a data volta do banco como "Unspecified" e o JSON sai sem o "Z",
/// fazendo o navegador achar que é horário local. Aqui marcamos toda data lida como UTC.
/// </summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    toDatabase => toDatabase,
    fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));
