using AdegaSystem.Api.Common;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Dashboard;
using AdegaSystem.Api.DTOs.Reports;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Monta a tela inicial. Os números de vendas vêm do ReportService (a mesma conta dos relatórios,
/// então dashboard e relatório nunca discordam); os de estoque e validade, de consultas diretas.
/// </summary>
public class DashboardService(AdegaDbContext context, IReportService reportService) : IDashboardService
{
    /// <summary>"Próximo do vencimento" no card: vence de hoje até daqui a 30 dias.</summary>
    public const int ExpiringDays = 30;

    public async Task<DashboardDto> GetAsync()
    {
        var today = BusinessClock.Today;
        var firstDayOfMonth = BusinessClock.FirstDayOfMonth(today);
        var last30From = today.AddDays(-29);

        // Uma consulta de vendas por gráfico. O último dia da série de 30 dias é "hoje".
        var last30Days = await reportService.GetSalesAsync(new SalesReportQuery { From = last30From, To = today });
        var last12Months = await reportService.GetSalesAsync(new SalesReportQuery
        {
            From = firstDayOfMonth.AddMonths(-11),
            To = today,
            GroupBy = ReportGroupBy.Month,
        });
        var month = await reportService.GetSalesAsync(new SalesReportQuery { From = firstDayOfMonth, To = today });
        var topProducts = await reportService.GetProductRankingAsync(new ProductRankingQuery { From = last30From, To = today, Limit = 5 });
        var categories = await reportService.GetCategoriesAsync(new ReportPeriodQuery { From = last30From, To = today });

        var todaySales = last30Days.Periods[^1];

        // Os mesmos critérios das telas de Produtos e Lotes, para o card bater com a lista ao clicar.
        var products = context.Products.AsNoTracking().Where(p => p.Active);
        var batchesWithStock = context.ProductBatches.AsNoTracking().Where(b => b.QuantityAvailable > 0);
        var expiringLimit = today.AddDays(ExpiringDays);

        var cards = new DashboardCardsDto(
            todaySales.SalesCount,
            todaySales.Revenue,
            month.Summary.Revenue,
            month.Summary.Profit,
            await products.CountAsync(),
            await products.SumAsync(p => p.CurrentStock),
            await products.CountAsync(p => p.CurrentStock <= p.MinimumStock),
            await products.CountAsync(p => p.CurrentStock == 0),
            // Produtos distintos: três lotes do mesmo vinho vencendo contam como um produto.
            await batchesWithStock
                .Where(b => b.ExpirationDate >= today && b.ExpirationDate <= expiringLimit)
                .Select(b => b.ProductId).Distinct().CountAsync(),
            await batchesWithStock
                .Where(b => b.ExpirationDate < today)
                .Select(b => b.ProductId).Distinct().CountAsync(),
            ExpiringDays);

        return new DashboardDto(
            cards,
            last30Days.Periods,
            last12Months.Periods,
            topProducts.Items,
            categories.Items,
            month.PaymentMethods);
    }
}
