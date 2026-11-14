using AdegaSystem.Api.Common;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.DTOs.Audit;

/// <summary>Uma linha da auditoria. OldValue e NewValue são o JSON gravado (o frontend formata).</summary>
public record AuditLogDto(
    int Id,
    DateTime CreatedAt,
    int UserId,
    string UserName,
    AuditAction Action,
    string Entity,
    int EntityId,
    string Description,
    string? OldValue,
    string? NewValue);

/// <summary>Filtros do GET /api/audit-logs. Datas no horário de Brasília.</summary>
public class AuditLogQuery : PagedQuery
{
    public string? Entity { get; set; }
    public int? EntityId { get; set; }
    public AuditAction? Action { get; set; }
    public int? UserId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
