using AdegaSystem.Api.Common;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Reports;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Mappings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Relatórios. Todos seguem a mesma receita:
///   1. transformar o período (dias de Brasília) num intervalo UTC, porque o banco guarda UTC;
///   2. buscar no banco só as colunas necessárias, já filtradas (vendas COMPLETED, período);
///   3. somar e agrupar em C#.
///
/// Por que agrupar em C# e não em SQL? Porque "o dia" de uma venda depende do fuso de Brasília,
/// e porque o código fica fácil de ler e de testar. Para o volume de uma adega (milhares de
/// vendas por mês) isso é rápido. Se um dia forem milhões de linhas, a soma vai para o SQL.
///
/// Venda cancelada nunca entra em faturamento, lucro ou ranking.
/// </summary>
public class ReportService(
    AdegaDbContext context,
    ISettingsService settingsService,
    IValidator<ReportPeriodQuery> periodValidator,
    IValidator<SalesReportQuery> salesValidator,
    IValidator<ProductRankingQuery> rankingValidator,
    IValidator<IdleProductsQuery> idleValidator) : IReportService
{
    /// <summary>Período padrão quando o filtro vem vazio: os últimos 30 dias, contando hoje.</summary>
    public const int DefaultPeriodDays = 30;

    public async Task<SalesReportDto> GetSalesAsync(SalesReportQuery query)
    {
        await salesValidator.ValidateAndThrowAsync(query);
        var (from, to) = ResolvePeriod(query);
        var sales = await LoadSalesAsync(from, to);

        // Uma linha para cada dia (ou mês) do período, mesmo sem venda: o gráfico não "pula" dias.
        var periods = PeriodKeys(from, to, query.GroupBy)
            .Select(key => ToPeriodDto(key, sales.Where(s => PeriodKey(s.LocalDate, query.GroupBy) == key).ToList()))
            .ToList();

        return new SalesReportDto(from, to, query.GroupBy, Summarize(sales), periods, ByPaymentMethod(sales));
    }

    public async Task<PeriodReportDto<ProductSalesDto>> GetProductRankingAsync(ProductRankingQuery query)
    {
        await rankingValidator.ValidateAndThrowAsync(query);
        var (from, to) = ResolvePeriod(query);
        var products = SumByProduct(await LoadItemsAsync(from, to));

        // Mais vendidos: maior quantidade primeiro (empate: maior faturamento).
        // Menos vendidos: só entre os que venderam algo; quem não vendeu nada está em "produtos parados".
        var ordered = query.Order == ProductRankingOrder.MostSold
            ? products.OrderByDescending(p => p.Quantity).ThenByDescending(p => p.Revenue)
            : products.OrderBy(p => p.Quantity).ThenBy(p => p.Revenue);

        return new PeriodReportDto<ProductSalesDto>(from, to, ordered.ThenBy(p => p.ProductName).Take(query.Limit).ToList());
    }

    public async Task<PeriodReportDto<CategorySalesDto>> GetCategoriesAsync(ReportPeriodQuery query)
    {
        await periodValidator.ValidateAndThrowAsync(query);
        var (from, to) = ResolvePeriod(query);
        var items = await LoadItemsAsync(from, to);
        var totalRevenue = items.Sum(i => i.NetTotal);

        var categories = items
            .GroupBy(i => new { i.CategoryId, i.CategoryName })
            .Select(g =>
            {
                var revenue = g.Sum(i => i.NetTotal);
                return new CategorySalesDto(
                    g.Key.CategoryId,
                    g.Key.CategoryName,
                    g.Sum(i => i.Quantity),
                    Money(revenue),
                    Money(revenue - g.Sum(i => i.Cost)),
                    Percent(revenue, totalRevenue));
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();

        return new PeriodReportDto<CategorySalesDto>(from, to, categories);
    }

    public async Task<IReadOnlyList<IdleProductDto>> GetIdleProductsAsync(IdleProductsQuery query)
    {
        await idleValidator.ValidateAndThrowAsync(query);
        var today = BusinessClock.Today;

        // "Parado no estoque": produto ativo, com estoque, e a data da última venda concluída
        // (uma subconsulta por produto, que o EF transforma num único SELECT).
        var products = await context.Products.AsNoTracking()
            .Where(p => p.Active && p.CurrentStock > 0)
            .Select(p => new
            {
                p.Id,
                p.Name,
                CategoryName = p.Category.Name,
                p.CurrentStock,
                p.CostPrice,
                p.CreatedAt,
                LastSaleDate = context.SaleItems
                    .Where(i => i.ProductId == p.Id && i.Sale.Status == SaleStatus.COMPLETED)
                    .Max(i => (DateTime?)i.Sale.SaleDate),
            })
            .ToListAsync();

        return products
            .Select(p => new IdleProductDto(
                p.Id,
                p.Name,
                p.CategoryName,
                p.CurrentStock,
                Money(p.CurrentStock * p.CostPrice),
                p.LastSaleDate,
                // Nunca vendido: conta desde o cadastro, para um produto que chegou ontem não aparecer como "parado".
                today.DayNumber - BusinessClock.ToBusinessDate(p.LastSaleDate ?? p.CreatedAt).DayNumber))
            .Where(p => p.DaysWithoutSale >= query.Days)
            .OrderByDescending(p => p.DaysWithoutSale)
            .ThenByDescending(p => p.StockCostValue)
            .ToList();
    }

    public async Task<MarginReportDto> GetMarginsAsync(MarginReportQuery query)
    {
        var minimum = (await settingsService.GetMarginAsync()).MinimumMarginPercent;

        var products = await context.Products.AsNoTracking()
            .Where(p => p.Active)
            .Select(p => new { p.Id, p.Name, CategoryName = p.Category.Name, p.CostPrice, p.SalePrice })
            .ToListAsync();

        // A mesma fórmula da tela de produtos: ((venda - custo) / venda) × 100.
        var items = products
            .Select(p =>
            {
                var margin = ProductMappings.CalculateMarginPercent(p.CostPrice, p.SalePrice);
                return new MarginProductDto(p.Id, p.Name, p.CategoryName, p.CostPrice, p.SalePrice, margin, margin < minimum);
            })
            .ToList();

        var filtered = items
            .Where(p => !query.OnlyBelowMinimum || p.BelowMinimum)
            .OrderBy(p => p.MarginPercent)
            .ThenBy(p => p.ProductName)
            .ToList();

        return new MarginReportDto(minimum, items.Count(p => p.BelowMinimum), filtered);
    }

    public async Task<PeriodReportDto<SupplierPurchasesDto>> GetPurchasesBySupplierAsync(ReportPeriodQuery query)
    {
        await periodValidator.ValidateAndThrowAsync(query);
        var (from, to) = ResolvePeriod(query);

        // PurchaseDate já é um dia (DateOnly, a data da nota), então não precisa converter fuso.
        var purchases = await context.Purchases.AsNoTracking()
            .Where(p => p.PurchaseDate >= from && p.PurchaseDate <= to)
            .Select(p => new { p.SupplierId, SupplierName = p.Supplier.Name, p.PurchaseDate, p.TotalAmount })
            .ToListAsync();

        var total = purchases.Sum(p => p.TotalAmount);
        var suppliers = purchases
            .GroupBy(p => new { p.SupplierId, p.SupplierName })
            .Select(g => new SupplierPurchasesDto(
                g.Key.SupplierId,
                g.Key.SupplierName,
                g.Count(),
                Money(g.Sum(p => p.TotalAmount)),
                g.Max(p => p.PurchaseDate),
                Percent(g.Sum(p => p.TotalAmount), total)))
            .OrderByDescending(s => s.TotalAmount)
            .ToList();

        return new PeriodReportDto<SupplierPurchasesDto>(from, to, suppliers);
    }

    public async Task<StockReportDto> GetStockAsync()
    {
        var products = await context.Products.AsNoTracking()
            .Where(p => p.Active)
            .Select(p => new { p.CategoryId, CategoryName = p.Category.Name, p.CurrentStock, p.CostPrice, p.SalePrice })
            .ToListAsync();

        var categories = products
            .GroupBy(p => new { p.CategoryId, p.CategoryName })
            .Select(g => new StockCategoryDto(
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Count(),
                g.Sum(p => p.CurrentStock),
                Money(g.Sum(p => p.CurrentStock * p.CostPrice)),
                Money(g.Sum(p => p.CurrentStock * p.SalePrice))))
            .OrderByDescending(c => c.CostValue)
            .ThenBy(c => c.CategoryName)
            .ToList();

        return new StockReportDto(
            products.Count,
            products.Sum(p => p.CurrentStock),
            Money(products.Sum(p => p.CurrentStock * p.CostPrice)),
            Money(products.Sum(p => p.CurrentStock * p.SalePrice)),
            categories);
    }

    // ---------- Consultas base ----------

    /// <summary>Uma venda concluída, só com o que os relatórios usam, e o dia dela em Brasília.</summary>
    private record SaleRow(DateOnly LocalDate, decimal Discount, decimal Total, decimal TotalCost, decimal Profit, PaymentMethod PaymentMethod);

    private async Task<List<SaleRow>> LoadSalesAsync(DateOnly from, DateOnly to)
    {
        var (start, end) = UtcRange(from, to);
        var rows = await context.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.COMPLETED && s.SaleDate >= start && s.SaleDate < end)
            .Select(s => new { s.SaleDate, s.Discount, s.Total, s.TotalCost, s.Profit, s.PaymentMethod })
            .ToListAsync();

        return rows
            .Select(s => new SaleRow(BusinessClock.ToBusinessDate(s.SaleDate), s.Discount, s.Total, s.TotalCost, s.Profit, s.PaymentMethod))
            .ToList();
    }

    /// <summary>
    /// Um item vendido. NetTotal = total do item menos a parte dele no desconto da venda inteira,
    /// rateado pelo valor: numa venda de R$ 40 com R$ 4 de desconto, um item de R$ 30 fica com R$ 27.
    /// </summary>
    private record ItemRow(int ProductId, string ProductName, int CategoryId, string CategoryName, int Quantity, decimal NetTotal, decimal Cost);

    private async Task<List<ItemRow>> LoadItemsAsync(DateOnly from, DateOnly to)
    {
        var (start, end) = UtcRange(from, to);
        var rows = await context.SaleItems.AsNoTracking()
            .Where(i => i.Sale.Status == SaleStatus.COMPLETED && i.Sale.SaleDate >= start && i.Sale.SaleDate < end)
            .Select(i => new
            {
                i.ProductId,
                ProductName = i.Product.Name,
                i.Product.CategoryId,
                CategoryName = i.Product.Category.Name,
                i.Quantity,
                i.Total,
                i.CostPrice,
                SaleSubtotal = i.Sale.Subtotal,
                SaleTotal = i.Sale.Total,
            })
            .ToListAsync();

        return rows
            .Select(i => new ItemRow(
                i.ProductId,
                i.ProductName,
                i.CategoryId,
                i.CategoryName,
                i.Quantity,
                i.SaleSubtotal == 0 ? 0 : i.Total * i.SaleTotal / i.SaleSubtotal,
                // CostPrice do item já é o custo médio real dos lotes usados (gravado na venda).
                i.Quantity * i.CostPrice))
            .ToList();
    }

    // ---------- Contas ----------

    private static SalesSummaryDto Summarize(IReadOnlyCollection<SaleRow> sales)
    {
        var revenue = sales.Sum(s => s.Total);
        var profit = sales.Sum(s => s.Profit);
        return new SalesSummaryDto(
            sales.Count,
            Money(revenue),
            Money(sales.Sum(s => s.Discount)),
            Money(sales.Sum(s => s.TotalCost)),
            Money(profit),
            sales.Count == 0 ? 0 : Money(revenue / sales.Count),
            Percent(profit, revenue));
    }

    private static SalesPeriodDto ToPeriodDto(DateOnly period, IReadOnlyCollection<SaleRow> sales)
    {
        var revenue = sales.Sum(s => s.Total);
        var profit = sales.Sum(s => s.Profit);
        return new SalesPeriodDto(period, sales.Count, Money(revenue), Money(sales.Sum(s => s.TotalCost)), Money(profit), Percent(profit, revenue));
    }

    private static List<PaymentMethodSalesDto> ByPaymentMethod(IReadOnlyCollection<SaleRow> sales)
    {
        var total = sales.Sum(s => s.Total);
        return sales
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new PaymentMethodSalesDto(g.Key, g.Count(), Money(g.Sum(s => s.Total)), Percent(g.Sum(s => s.Total), total)))
            .OrderByDescending(p => p.Revenue)
            .ToList();
    }

    private static List<ProductSalesDto> SumByProduct(IEnumerable<ItemRow> items) =>
        items
            .GroupBy(i => new { i.ProductId, i.ProductName, i.CategoryName })
            .Select(g =>
            {
                var revenue = g.Sum(i => i.NetTotal);
                var cost = g.Sum(i => i.Cost);
                return new ProductSalesDto(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.CategoryName,
                    g.Sum(i => i.Quantity),
                    Money(revenue),
                    Money(cost),
                    Money(revenue - cost),
                    Percent(revenue - cost, revenue));
            })
            .ToList();

    // ---------- Datas ----------

    private static (DateOnly From, DateOnly To) ResolvePeriod(ReportPeriodQuery query)
    {
        var to = query.To ?? BusinessClock.Today;
        var from = query.From ?? to.AddDays(-(DefaultPeriodDays - 1));
        return (from, to);
    }

    /// <summary>Do início do dia "from" até o início do dia seguinte a "to", em Brasília, convertido para UTC.</summary>
    private static (DateTime Start, DateTime End) UtcRange(DateOnly from, DateOnly to) =>
        (BusinessClock.StartOfDayUtc(from), BusinessClock.StartOfDayUtc(to.AddDays(1)));

    private static DateOnly PeriodKey(DateOnly date, ReportGroupBy groupBy) =>
        groupBy == ReportGroupBy.Month ? BusinessClock.FirstDayOfMonth(date) : date;

    private static IEnumerable<DateOnly> PeriodKeys(DateOnly from, DateOnly to, ReportGroupBy groupBy)
    {
        for (var key = PeriodKey(from, groupBy); key <= to; key = groupBy == ReportGroupBy.Month ? key.AddMonths(1) : key.AddDays(1))
        {
            yield return key;
        }
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>part / whole × 100, com 2 casas (0 quando não há total).</summary>
    private static decimal Percent(decimal part, decimal whole) =>
        whole == 0 ? 0 : Math.Round(part / whole * 100, 2, MidpointRounding.AwayFromZero);
}
