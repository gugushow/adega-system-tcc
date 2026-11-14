using AdegaSystem.Api.DTOs.Reports;

namespace AdegaSystem.Api.Services;

public interface IReportService
{
    /// <summary>Vendas, faturamento, custo (CMV), lucro e margem do período, por dia ou por mês.</summary>
    Task<SalesReportDto> GetSalesAsync(SalesReportQuery query);

    Task<PeriodReportDto<ProductSalesDto>> GetProductRankingAsync(ProductRankingQuery query);
    Task<PeriodReportDto<CategorySalesDto>> GetCategoriesAsync(ReportPeriodQuery query);
    Task<IReadOnlyList<IdleProductDto>> GetIdleProductsAsync(IdleProductsQuery query);
    Task<MarginReportDto> GetMarginsAsync(MarginReportQuery query);
    Task<PeriodReportDto<SupplierPurchasesDto>> GetPurchasesBySupplierAsync(ReportPeriodQuery query);
    Task<StockReportDto> GetStockAsync();
}
