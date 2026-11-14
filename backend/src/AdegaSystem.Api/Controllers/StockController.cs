using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = Roles.AdminOrEstoquista)]
public class StockController(IStockService stockService) : ControllerBase
{
    /// <summary>Extrato de movimentações, mais recentes primeiro. Filtros: productId, type, from, to.</summary>
    [HttpGet("stock-movements")]
    public async Task<ActionResult<ApiResponse<PagedResult<StockMovementDto>>>> GetMovements([FromQuery] StockMovementQuery query) =>
        Ok(ApiResponse<PagedResult<StockMovementDto>>.Ok(await stockService.GetMovementsAsync(query)));

    /// <summary>Ajuste manual (ADJUSTMENT, + ou −) ou perda (LOSS, sempre negativa). O motivo é obrigatório.</summary>
    [HttpPost("stock/adjustments")]
    public async Task<ActionResult<ApiResponse<StockMovementDto>>> Adjust(StockAdjustmentDto dto) =>
        Ok(ApiResponse<StockMovementDto>.Ok(await stockService.AdjustAsync(dto), "Ajuste de estoque registrado."));
}
