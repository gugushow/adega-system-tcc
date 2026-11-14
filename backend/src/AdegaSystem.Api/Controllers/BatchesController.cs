using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Batches;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

[ApiController]
[Route("api/batches")]
[Authorize(Roles = Roles.AdminOrEstoquista)]
public class BatchesController(IBatchService batchService) : ControllerBase
{
    /// <summary>Lotes ordenados pela validade (sem validade no fim). Filtros: productId, onlyAvailable, status, days.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<BatchDto>>>> GetAll([FromQuery] BatchQuery query) =>
        Ok(ApiResponse<PagedResult<BatchDto>>.Ok(await batchService.GetAllAsync(query)));

    /// <summary>Lotes vencidos que ainda têm saldo.</summary>
    [HttpGet("expired")]
    public Task<ActionResult<ApiResponse<PagedResult<BatchDto>>>> GetExpired([FromQuery] BatchQuery query)
    {
        query.Status = BatchStatusFilter.Expired;
        return GetAll(query);
    }

    /// <summary>Lotes que vencem de hoje até daqui a "days" dias (padrão 30).</summary>
    [HttpGet("expiring")]
    public Task<ActionResult<ApiResponse<PagedResult<BatchDto>>>> GetExpiring([FromQuery] BatchQuery query)
    {
        query.Status = BatchStatusFilter.Expiring;
        return GetAll(query);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<BatchDto>>> GetById(int id) =>
        Ok(ApiResponse<BatchDto>.Ok(await batchService.GetByIdAsync(id)));

    /// <summary>Baixa de lote vencido: zera o saldo e grava uma movimentação EXPIRED.</summary>
    [HttpPost("{id:int}/write-off")]
    public async Task<ActionResult<ApiResponse<BatchDto>>> WriteOff(int id, WriteOffBatchDto dto) =>
        Ok(ApiResponse<BatchDto>.Ok(await batchService.WriteOffAsync(id, dto), "Baixa do lote vencido registrada."));
}
