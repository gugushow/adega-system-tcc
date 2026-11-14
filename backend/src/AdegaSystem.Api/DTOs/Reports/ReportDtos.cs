using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.DTOs.Reports;

// ---------- Filtros ----------

/// <summary>
/// Período de um relatório, em dias do horário de Brasília (ex.: from=2026-10-01&amp;to=2026-10-31).
/// Sem datas: os últimos 30 dias até hoje.
/// </summary>
public class ReportPeriodQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public enum ReportGroupBy { Day, Month }

/// <summary>Filtros do relatório de vendas: o período e se os totais vêm por dia ou por mês.</summary>
public class SalesReportQuery : ReportPeriodQuery
{
    public ReportGroupBy GroupBy { get; set; } = ReportGroupBy.Day;
}

public enum ProductRankingOrder { MostSold, LeastSold }

/// <summary>Filtros do ranking de produtos: mais vendidos ou menos vendidos (só os que venderam algo).</summary>
public class ProductRankingQuery : ReportPeriodQuery
{
    public ProductRankingOrder Order { get; set; } = ProductRankingOrder.MostSold;
    public int Limit { get; set; } = 20;
}

/// <summary>Produtos parados: sem venda há pelo menos N dias (30, 60 ou 90 na tela).</summary>
public class IdleProductsQuery
{
    public int Days { get; set; } = 30;
}

public class MarginReportQuery
{
    public bool OnlyBelowMinimum { get; set; }
}

// ---------- Respostas ----------

/// <summary>Resposta padrão dos relatórios por período: o período usado (já com os padrões aplicados) e as linhas.</summary>
public record PeriodReportDto<T>(DateOnly From, DateOnly To, IReadOnlyList<T> Items);

/// <summary>
/// Totais das vendas concluídas (canceladas nunca entram).
/// Revenue = faturamento (soma dos totais, já com desconto). Cost = custo das mercadorias vendidas (CMV).
/// MarginPercent = Profit / Revenue × 100 (margem média ponderada pelo faturamento).
/// </summary>
public record SalesSummaryDto(
    int SalesCount,
    decimal Revenue,
    decimal Discounts,
    decimal Cost,
    decimal Profit,
    decimal AverageTicket,
    decimal MarginPercent);

/// <summary>Um dia ou um mês do relatório. Period é o dia, ou o dia 1 do mês quando agrupado por mês.</summary>
public record SalesPeriodDto(DateOnly Period, int SalesCount, decimal Revenue, decimal Cost, decimal Profit, decimal MarginPercent);

public record PaymentMethodSalesDto(PaymentMethod PaymentMethod, int SalesCount, decimal Revenue, decimal Percent);

/// <summary>Vendas, faturamento e lucro de um período, com a linha do tempo e as formas de pagamento.</summary>
public record SalesReportDto(
    DateOnly From,
    DateOnly To,
    ReportGroupBy GroupBy,
    SalesSummaryDto Summary,
    IReadOnlyList<SalesPeriodDto> Periods,
    IReadOnlyList<PaymentMethodSalesDto> PaymentMethods);

/// <summary>
/// Quanto um produto vendeu no período. Revenue já desconta a parte dele no desconto da venda
/// (rateado pelo valor de cada item), então a soma dos produtos bate com o faturamento.
/// </summary>
public record ProductSalesDto(
    int ProductId,
    string ProductName,
    string CategoryName,
    int Quantity,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal MarginPercent);

/// <summary>Vendas por categoria. Percent = participação no faturamento do período.</summary>
public record CategorySalesDto(int CategoryId, string CategoryName, int Quantity, decimal Revenue, decimal Profit, decimal Percent);

/// <summary>
/// Produto com estoque e sem venda recente. StockCostValue = estoque × custo: o dinheiro parado na prateleira.
/// Nunca vendido: LastSaleDate nulo e os dias contam desde o cadastro do produto.
/// </summary>
public record IdleProductDto(
    int ProductId,
    string ProductName,
    string CategoryName,
    int CurrentStock,
    decimal StockCostValue,
    DateTime? LastSaleDate,
    int DaysWithoutSale);

public record MarginProductDto(
    int ProductId,
    string ProductName,
    string CategoryName,
    decimal CostPrice,
    decimal SalePrice,
    decimal MarginPercent,
    bool BelowMinimum);

/// <summary>Margem de cada produto ativo comparada com a margem mínima configurada.</summary>
public record MarginReportDto(decimal MinimumMarginPercent, int BelowMinimumCount, IReadOnlyList<MarginProductDto> Items);

/// <summary>Compras de um fornecedor no período. Percent = participação no total comprado.</summary>
public record SupplierPurchasesDto(
    int SupplierId,
    string SupplierName,
    int PurchaseCount,
    decimal TotalAmount,
    DateOnly LastPurchaseDate,
    decimal Percent);

/// <summary>Estoque atual de uma categoria: unidades, valor pelo custo e valor pelo preço de venda.</summary>
public record StockCategoryDto(int CategoryId, string CategoryName, int ProductCount, int Units, decimal CostValue, decimal SaleValue);

public record StockReportDto(int ProductCount, int Units, decimal CostValue, decimal SaleValue, IReadOnlyList<StockCategoryDto> Categories);
