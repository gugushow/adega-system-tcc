using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Customers;
using AdegaSystem.Api.DTOs.Loyalty;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = Roles.AdminOrVendedor)]
public class CustomersController(ICustomerService customerService, ISaleService saleService, ILoyaltyService loyaltyService) : ControllerBase
{
    /// <summary>Lista paginada. Filtros: search (nome, CPF ou telefone) e active.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerDto>>>> GetAll([FromQuery] CustomerQuery query) =>
        Ok(ApiResponse<PagedResult<CustomerDto>>.Ok(await customerService.GetAllAsync(query)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetById(int id) =>
        Ok(ApiResponse<CustomerDto>.Ok(await customerService.GetByIdAsync(id)));

    /// <summary>Número de compras, total gasto, última compra e ticket médio (só vendas concluídas).</summary>
    [HttpGet("{id:int}/summary")]
    public async Task<ActionResult<ApiResponse<CustomerSummaryDto>>> GetSummary(int id) =>
        Ok(ApiResponse<CustomerSummaryDto>.Ok(await customerService.GetSummaryAsync(id)));

    /// <summary>Histórico de vendas do cliente (mesma listagem de /api/sales, já filtrada).</summary>
    [HttpGet("{id:int}/sales")]
    public async Task<ActionResult<ApiResponse<PagedResult<SaleDto>>>> GetSales(int id, [FromQuery] SaleQuery query)
    {
        await customerService.GetByIdAsync(id); // 404 se o cliente não existir
        query.CustomerId = id;
        return Ok(ApiResponse<PagedResult<SaleDto>>.Ok(await saleService.GetAllAsync(query)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Create(CreateCustomerDto dto)
    {
        var customer = await customerService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id },
            ApiResponse<CustomerDto>.Ok(customer, "Cliente cadastrado com sucesso."));
    }

    /// <summary>ADMIN e VENDEDOR editam; só o ADMIN muda a situação (ativo/inativo).</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Update(int id, UpdateCustomerDto dto) =>
        Ok(ApiResponse<CustomerDto>.Ok(
            await customerService.UpdateAsync(id, dto, canChangeStatus: User.IsInRole(Roles.Admin)),
            "Cliente atualizado com sucesso."));

    /// <summary>Não apaga do banco: desativa o cliente (soft delete).</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(int id)
    {
        await customerService.DeactivateAsync(id);
        return Ok(ApiResponse.Ok("Cliente desativado com sucesso."));
    }

    /// <summary>Saldo de pontos, regra atual do programa e o extrato (paginado, mais recentes primeiro).</summary>
    [HttpGet("{id:int}/loyalty")]
    public async Task<ActionResult<ApiResponse<LoyaltyStatementDto>>> GetLoyalty(int id, [FromQuery] PagedQuery query) =>
        Ok(ApiResponse<LoyaltyStatementDto>.Ok(await loyaltyService.GetStatementAsync(id, query)));

    /// <summary>Troca de pontos (brinde, desconto combinado no balcão...). Tira do saldo.</summary>
    [HttpPost("{id:int}/loyalty/redeem")]
    public async Task<ActionResult<ApiResponse<LoyaltyStatementDto>>> RedeemPoints(int id, RedeemPointsDto dto) =>
        Ok(ApiResponse<LoyaltyStatementDto>.Ok(await loyaltyService.RedeemAsync(id, dto), "Resgate registrado com sucesso."));

    /// <summary>Correção manual do saldo (só ADMIN). Entra na auditoria.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:int}/loyalty/adjust")]
    public async Task<ActionResult<ApiResponse<LoyaltyStatementDto>>> AdjustPoints(int id, AdjustPointsDto dto) =>
        Ok(ApiResponse<LoyaltyStatementDto>.Ok(await loyaltyService.AdjustAsync(id, dto), "Ajuste de pontos registrado."));
}
