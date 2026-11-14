using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Products;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService productService) : ControllerBase
{
    /// <summary>Lista paginada. Filtros: search, categoryId, active, stock (Low ou Out).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetAll([FromQuery] ProductQuery query) =>
        Ok(ApiResponse<PagedResult<ProductDto>>.Ok(await productService.GetAllAsync(query)));

    /// <summary>Produtos ativos com CurrentStock &lt;= MinimumStock.</summary>
    [HttpGet("low-stock")]
    public Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetLowStock([FromQuery] ProductQuery query)
    {
        query.Stock = StockFilter.Low;
        return GetAll(query);
    }

    /// <summary>Produtos ativos com estoque zerado.</summary>
    [HttpGet("out-of-stock")]
    public Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetOutOfStock([FromQuery] ProductQuery query)
    {
        query.Stock = StockFilter.Out;
        return GetAll(query);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(int id) =>
        Ok(ApiResponse<ProductDto>.Ok(await productService.GetByIdAsync(id)));

    /// <summary>Usado pelo leitor de código de barras (na tela de produtos e no PDV).</summary>
    [HttpGet("barcode/{barcode}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetByBarcode(string barcode) =>
        Ok(ApiResponse<ProductDto>.Ok(await productService.GetByBarcodeAsync(barcode)));

    [Authorize(Roles = Roles.AdminOrEstoquista)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create(CreateProductDto dto)
    {
        var product = await productService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = product.Id },
            ApiResponse<ProductDto>.Ok(product, "Produto cadastrado com sucesso."));
    }

    [Authorize(Roles = Roles.AdminOrEstoquista)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Update(int id, UpdateProductDto dto) =>
        Ok(ApiResponse<ProductDto>.Ok(await productService.UpdateAsync(id, dto), "Produto atualizado com sucesso."));

    /// <summary>Não apaga do banco: desativa o produto (soft delete).</summary>
    [Authorize(Roles = Roles.AdminOrEstoquista)]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(int id)
    {
        await productService.DeactivateAsync(id);
        return Ok(ApiResponse.Ok("Produto desativado com sucesso."));
    }
}
