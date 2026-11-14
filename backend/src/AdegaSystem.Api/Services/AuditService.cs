using System.Linq.Expressions;
using System.Text.Json;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Audit;
using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Log de auditoria. Os services chamam Add() explicitamente nas ações importantes,
/// em vez de um mecanismo automático que gravasse toda e qualquer alteração: assim cada registro
/// tem um significado claro ("preço alterado", "venda cancelada") e só os campos que importam.
/// </summary>
public class AuditService(AdegaDbContext context, ICurrentUserService currentUser) : IAuditService
{
    // camelCase, igual ao JSON da API: {"salePrice":6.50}.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add(AuditAction action, string entity, int entityId, string description, object? oldValue = null, object? newValue = null)
    {
        context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Description = description.Length > 200 ? description[..200] : description,
            OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
        });
    }

    public async Task<PagedResult<AuditLogDto>> GetAllAsync(AuditLogQuery query)
    {
        var logs = context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Entity))
        {
            logs = logs.Where(a => a.Entity == query.Entity);
        }

        if (query.EntityId.HasValue)
        {
            logs = logs.Where(a => a.EntityId == query.EntityId.Value);
        }

        if (query.Action.HasValue)
        {
            logs = logs.Where(a => a.Action == query.Action.Value);
        }

        if (query.UserId.HasValue)
        {
            logs = logs.Where(a => a.UserId == query.UserId.Value);
        }

        // O dia é o de Brasília (ver BusinessClock). "To" inclui o dia inteiro.
        if (query.From.HasValue)
        {
            var start = BusinessClock.StartOfDayUtc(query.From.Value);
            logs = logs.Where(a => a.CreatedAt >= start);
        }

        if (query.To.HasValue)
        {
            var end = BusinessClock.StartOfDayUtc(query.To.Value.AddDays(1));
            logs = logs.Where(a => a.CreatedAt < end);
        }

        var totalItems = await logs.CountAsync();

        var page = await logs
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync();

        return new PagedResult<AuditLogDto>(page, query.Page, query.PageSize, totalItems);
    }

    private static readonly Expression<Func<AuditLog, AuditLogDto>> ToDto = a => new AuditLogDto(
        a.Id, a.CreatedAt, a.UserId, a.User.Name, a.Action, a.Entity, a.EntityId, a.Description, a.OldValue, a.NewValue);
}
