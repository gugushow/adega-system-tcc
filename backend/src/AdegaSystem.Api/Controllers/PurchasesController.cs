using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Purchases;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>Compras não têm PUT nem DELETE: são documentos e nunca são editadas ou apagadas.</summary>
[ApiController]
[Route("api/purchases")]
[Authorize(Roles = Roles.AdminOrEstoquista)]
public class PurchasesController(IPurchaseService purchaseService) : ControllerBase
{
    /// <summary>Lista paginada. Filtros: supplierId, from e to (datas da compra, ex.: 2026-10-01).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PurchaseDto>>>> GetAll([FromQuery] PurchaseQuery query) =>
        Ok(ApiResponse<PagedResult<PurchaseDto>>.Ok(await purchaseService.GetAllAsync(query)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PurchaseDetailsDto>>> GetById(int id) =>
        Ok(ApiResponse<PurchaseDetailsDto>.Ok(await purchaseService.GetByIdAsync(id)));

    /// <summary>Registra a compra, dá entrada no estoque e atualiza o custo dos produtos.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PurchaseDetailsDto>>> Create(CreatePurchaseDto dto)
    {
        var purchase = await purchaseService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = purchase.Id },
            ApiResponse<PurchaseDetailsDto>.Ok(purchase, "Compra registrada e estoque atualizado."));
    }
}
