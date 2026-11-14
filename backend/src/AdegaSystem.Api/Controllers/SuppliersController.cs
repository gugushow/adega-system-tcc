using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Purchases;
using AdegaSystem.Api.DTOs.Suppliers;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize(Roles = Roles.AdminOrEstoquista)]
public class SuppliersController(ISupplierService supplierService, IPurchaseService purchaseService) : ControllerBase
{
    /// <summary>Lista paginada. Filtros: search (nome, razão social ou CNPJ) e active.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierDto>>>> GetAll([FromQuery] SupplierQuery query) =>
        Ok(ApiResponse<PagedResult<SupplierDto>>.Ok(await supplierService.GetAllAsync(query)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> GetById(int id) =>
        Ok(ApiResponse<SupplierDto>.Ok(await supplierService.GetByIdAsync(id)));

    /// <summary>Histórico de compras do fornecedor (mesma listagem de /api/purchases, já filtrada).</summary>
    [HttpGet("{id:int}/purchases")]
    public async Task<ActionResult<ApiResponse<PagedResult<PurchaseDto>>>> GetPurchases(int id, [FromQuery] PurchaseQuery query)
    {
        await supplierService.GetByIdAsync(id); // 404 se o fornecedor não existir
        query.SupplierId = id;
        return Ok(ApiResponse<PagedResult<PurchaseDto>>.Ok(await purchaseService.GetAllAsync(query)));
    }

    /// <summary>Total comprado, número de compras, última compra e produtos mais comprados.</summary>
    [HttpGet("{id:int}/summary")]
    public async Task<ActionResult<ApiResponse<SupplierSummaryDto>>> GetSummary(int id) =>
        Ok(ApiResponse<SupplierSummaryDto>.Ok(await supplierService.GetSummaryAsync(id)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Create(CreateSupplierDto dto)
    {
        var supplier = await supplierService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = supplier.Id },
            ApiResponse<SupplierDto>.Ok(supplier, "Fornecedor cadastrado com sucesso."));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Update(int id, UpdateSupplierDto dto) =>
        Ok(ApiResponse<SupplierDto>.Ok(await supplierService.UpdateAsync(id, dto), "Fornecedor atualizado com sucesso."));

    /// <summary>Não apaga do banco: desativa o fornecedor (soft delete).</summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(int id)
    {
        await supplierService.DeactivateAsync(id);
        return Ok(ApiResponse.Ok("Fornecedor desativado com sucesso."));
    }
}
