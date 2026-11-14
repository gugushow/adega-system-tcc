using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>
/// Vendas não têm PUT nem DELETE: são documentos. Para desfazer uma venda, ela é cancelada,
/// o que devolve as unidades aos lotes. Cancelar é só para o ADMIN.
/// </summary>
[ApiController]
[Route("api/sales")]
[Authorize(Roles = Roles.AdminOrVendedor)]
public class SalesController(ISaleService saleService) : ControllerBase
{
    /// <summary>Lista paginada. Filtros: from e to (dias, ex.: 2026-10-01), status, customerId e paymentMethod.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SaleDto>>>> GetAll([FromQuery] SaleQuery query) =>
        Ok(ApiResponse<PagedResult<SaleDto>>.Ok(await saleService.GetAllAsync(query)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<SaleDetailsDto>>> GetById(int id) =>
        Ok(ApiResponse<SaleDetailsDto>.Ok(await saleService.GetByIdAsync(id)));

    /// <summary>Finaliza a venda: escolhe os lotes por FEFO, dá baixa no estoque e calcula o lucro, numa transação.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<SaleDetailsDto>>> Create(CreateSaleDto dto)
    {
        var sale = await saleService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = sale.Id },
            ApiResponse<SaleDetailsDto>.Ok(sale, $"Venda #{sale.Id} finalizada."));
    }

    /// <summary>Cancela a venda e devolve cada unidade ao lote de onde saiu.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<SaleDetailsDto>>> Cancel(int id, CancelSaleDto dto) =>
        Ok(ApiResponse<SaleDetailsDto>.Ok(await saleService.CancelAsync(id, dto), $"Venda #{id} cancelada e estoque devolvido."));
}
