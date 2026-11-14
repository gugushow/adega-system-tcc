using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Reports;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>
/// Relatórios (só ADMIN). Os períodos usam dias no horário de Brasília (from e to, ex.: 2026-10-01);
/// sem datas, valem os últimos 30 dias. Vendas canceladas nunca entram.
/// As respostas são listas "planas" (uma linha por item), prontas para virar tabela,
/// gráfico ou, no futuro, um arquivo Excel/PDF.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.Admin)]
public class ReportsController(IReportService reportService) : ControllerBase
{
    /// <summary>Vendas, faturamento, CMV, lucro e margem por dia ou mês (groupBy=Day|Month), e formas de pagamento.</summary>
    [HttpGet("sales")]
    public async Task<ActionResult<ApiResponse<SalesReportDto>>> Sales([FromQuery] SalesReportQuery query) =>
        Ok(ApiResponse<SalesReportDto>.Ok(await reportService.GetSalesAsync(query)));

    /// <summary>Produtos mais vendidos (order=MostSold) ou menos vendidos (order=LeastSold), com faturamento e lucro.</summary>
    [HttpGet("products")]
    public async Task<ActionResult<ApiResponse<PeriodReportDto<ProductSalesDto>>>> Products([FromQuery] ProductRankingQuery query) =>
        Ok(ApiResponse<PeriodReportDto<ProductSalesDto>>.Ok(await reportService.GetProductRankingAsync(query)));

    /// <summary>Faturamento e lucro por categoria.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<PeriodReportDto<CategorySalesDto>>>> Categories([FromQuery] ReportPeriodQuery query) =>
        Ok(ApiResponse<PeriodReportDto<CategorySalesDto>>.Ok(await reportService.GetCategoriesAsync(query)));

    /// <summary>Produtos com estoque e sem venda há pelo menos "days" dias (30, 60, 90...).</summary>
    [HttpGet("idle-products")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<IdleProductDto>>>> IdleProducts([FromQuery] IdleProductsQuery query) =>
        Ok(ApiResponse<IReadOnlyList<IdleProductDto>>.Ok(await reportService.GetIdleProductsAsync(query)));

    /// <summary>Margem de cada produto ativo e quais estão abaixo da margem mínima configurada.</summary>
    [HttpGet("margins")]
    public async Task<ActionResult<ApiResponse<MarginReportDto>>> Margins([FromQuery] MarginReportQuery query) =>
        Ok(ApiResponse<MarginReportDto>.Ok(await reportService.GetMarginsAsync(query)));

    /// <summary>Total comprado de cada fornecedor no período (pela data da nota).</summary>
    [HttpGet("purchases-by-supplier")]
    public async Task<ActionResult<ApiResponse<PeriodReportDto<SupplierPurchasesDto>>>> PurchasesBySupplier([FromQuery] ReportPeriodQuery query) =>
        Ok(ApiResponse<PeriodReportDto<SupplierPurchasesDto>>.Ok(await reportService.GetPurchasesBySupplierAsync(query)));

    /// <summary>Estoque atual por categoria: unidades, valor pelo custo e pelo preço de venda.</summary>
    [HttpGet("stock")]
    public async Task<ActionResult<ApiResponse<StockReportDto>>> Stock() =>
        Ok(ApiResponse<StockReportDto>.Ok(await reportService.GetStockAsync()));
}
