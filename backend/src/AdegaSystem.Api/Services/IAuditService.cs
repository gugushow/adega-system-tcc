using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Audit;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Services;

public interface IAuditService
{
    /// <summary>
    /// Adiciona um registro de auditoria ao contexto, SEM salvar. Ele é gravado no próximo
    /// SaveChanges de quem chamou, junto (e na mesma transação) da alteração que está sendo registrada:
    /// se a operação falhar, o registro de auditoria também não fica.
    /// </summary>
    void Add(AuditAction action, string entity, int entityId, string description, object? oldValue = null, object? newValue = null);

    Task<PagedResult<AuditLogDto>> GetAllAsync(AuditLogQuery query);
}
