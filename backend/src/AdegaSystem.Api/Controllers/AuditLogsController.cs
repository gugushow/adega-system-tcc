using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Audit;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>Consulta da auditoria (só ADMIN). Não há POST, PUT nem DELETE: registros de auditoria só são criados pelos services.</summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = Roles.Admin)]
public class AuditLogsController(IAuditService auditService) : ControllerBase
{
    /// <summary>Filtros: entity, entityId, action, userId, from, to (dias no horário de Brasília).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> GetAll([FromQuery] AuditLogQuery query) =>
        Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(await auditService.GetAllAsync(query)));
}
