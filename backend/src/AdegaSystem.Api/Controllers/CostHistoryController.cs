using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.CostHistory;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>Fica fora do ProductsController para não misturar o cadastro com o histórico de compras.</summary>
[ApiController]
[Route("api/products/{productId:int}/cost-history")]
[Authorize(Roles = Roles.AdminOrEstoquista)]
public class CostHistoryController(ICostHistoryService costHistoryService) : ControllerBase
{
    /// <summary>Custos pagos em cada compra, em ordem de data, com a variação percentual.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<CostHistoryDto>>> Get(int productId) =>
        Ok(ApiResponse<CostHistoryDto>.Ok(await costHistoryService.GetByProductAsync(productId)));
}
