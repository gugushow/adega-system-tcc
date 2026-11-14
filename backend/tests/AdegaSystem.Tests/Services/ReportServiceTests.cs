using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Reports;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.DTOs.Settings;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;

namespace AdegaSystem.Tests.Services;

public class ReportServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private ReportService CreateService() => TestData.CreateReportService(_database.CreateContext());

    private static readonly DateOnly Sep01 = new(2026, 9, 1);
    private static readonly DateOnly Sep30 = new(2026, 9, 30);
    private static readonly DateTime Sep10Afternoon = new(2026, 9, 10, 15, 0, 0, DateTimeKind.Utc);

    /// <summary>Produto com 100 unidades num lote, ao custo informado.</summary>
    private async Task<Product> ProductWithStockAsync(string name, decimal cost, decimal price, int stock = 100, int categoryId = TestData.CervejaCategoryId)
    {
        var product = await TestData.AddProductAsync(_database, name, costPrice: cost, salePrice: price, categoryId: categoryId);
        if (stock > 0)
        {
            await TestData.ReceiveBatchAsync(_database, product.Id, stock, unitCost: cost);
        }
        return product;
    }

    /// <summary>Faz uma venda pelo SaleService (FEFO, baixa de estoque e lucro de verdade) e muda a data dela.</summary>
    private async Task<int> SellAsync(DateTime saleDateUtc, PaymentMethod method, decimal discount, params (int ProductId, int Quantity)[] items)
    {
        await using var context = _database.CreateContext();
        var sale = await TestData.CreateSaleService(context).CreateAsync(new CreateSaleDto(
            null, method, discount, items.Select(i => new CreateSaleItemDto(i.ProductId, i.Quantity)).ToList()));

        var entity = await context.Sales.FindAsync([sale.Id], TestContext.Current.CancellationToken);
        entity!.SaleDate = saleDateUtc;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return sale.Id;
    }

    private async Task CancelAsync(int saleId)
    {
        await using var context = _database.CreateContext();
        await TestData.CreateSaleService(context).CancelAsync(saleId, new CancelSaleDto("Teste de relatório"));
    }

    // ---------- Vendas, faturamento e lucro ----------

    [Fact]
    public async Task Sales_SumsRevenueCostAndProfit_AndIgnoresCanceledSales()
    {
        var beer = await ProductWithStockAsync("Heineken", cost: 4m, price: 10m);
        await SellAsync(Sep10Afternoon, PaymentMethod.PIX, 0m, (beer.Id, 3));      // 30 − 12 = 18
        await SellAsync(Sep10Afternoon, PaymentMethod.CASH, 2m, (beer.Id, 2));     // 18 − 8 = 10
        var canceled = await SellAsync(Sep10Afternoon, PaymentMethod.PIX, 0m, (beer.Id, 1));
        await CancelAsync(canceled);

        var report = await CreateService().GetSalesAsync(new SalesReportQuery { From = Sep01, To = Sep30 });

        Assert.Equal(new SalesSummaryDto(
            SalesCount: 2, Revenue: 48m, Discounts: 2m, Cost: 20m, Profit: 28m, AverageTicket: 24m, MarginPercent: 58.33m), report.Summary);
    }

    [Fact]
    public async Task Sales_GroupsByBrasiliaDay_AndFillsDaysWithoutSales()
    {
        var beer = await ProductWithStockAsync("Heineken", cost: 4m, price: 10m);
        // 11/09 01:30 UTC = 10/09 22:30 em Brasília: pertence ao dia 10.
        await SellAsync(new DateTime(2026, 9, 11, 1, 30, 0, DateTimeKind.Utc), PaymentMethod.PIX, 0m, (beer.Id, 1));
        await SellAsync(new DateTime(2026, 9, 12, 15, 0, 0, DateTimeKind.Utc), PaymentMethod.PIX, 0m, (beer.Id, 2));

        var report = await CreateService().GetSalesAsync(new SalesReportQuery { From = new(2026, 9, 10), To = new(2026, 9, 12) });

        Assert.Equal([new DateOnly(2026, 9, 10), new(2026, 9, 11), new(2026, 9, 12)], report.Periods.Select(p => p.Period));
        Assert.Equal([10m, 0m, 20m], report.Periods.Select(p => p.Revenue));
        Assert.Equal([1, 0, 1], report.Periods.Select(p => p.SalesCount));
    }

    [Fact]
    public async Task Sales_LastSaleOfTheDayInBrasilia_IsNotCountedOnTheNextDay()
    {
        var beer = await ProductWithStockAsync("Heineken", cost: 4m, price: 10m);
        await SellAsync(new DateTime(2026, 9, 11, 2, 59, 0, DateTimeKind.Utc), PaymentMethod.PIX, 0m, (beer.Id, 1)); // 10/09 23:59

        var day10 = await CreateService().GetSalesAsync(new SalesReportQuery { From = new(2026, 9, 10), To = new(2026, 9, 10) });
        var day11 = await CreateService().GetSalesAsync(new SalesReportQuery { From = new(2026, 9, 11), To = new(2026, 9, 11) });

        Assert.Equal(1, day10.Summary.SalesCount);
        Assert.Equal(0, day11.Summary.SalesCount);
    }

    [Fact]
    public async Task Sales_GroupedByMonth()
    {
        var beer = await ProductWithStockAsync("Heineken", cost: 4m, price: 10m);
        await SellAsync(new DateTime(2026, 8, 15, 15, 0, 0, DateTimeKind.Utc), PaymentMethod.PIX, 0m, (beer.Id, 1));
        await SellAsync(Sep10Afternoon, PaymentMethod.PIX, 0m, (beer.Id, 3));

        var report = await CreateService().GetSalesAsync(new SalesReportQuery { From = new(2026, 8, 1), To = Sep30, GroupBy = ReportGroupBy.Month });

        Assert.Equal([new DateOnly(2026, 8, 1), new(2026, 9, 1)], report.Periods.Select(p => p.Period));
        Assert.Equal([10m, 30m], report.Periods.Select(p => p.Revenue));
        Assert.Equal([6m, 18m], report.Periods.Select(p => p.Profit));
    }

    [Fact]
    public async Task Sales_PaymentMethodsWithShareOfRevenue()
    {
        var beer = await ProductWithStockAsync("Heineken", cost: 4m, price: 10m);
        await SellAsync(Sep10Afternoon, PaymentMethod.PIX, 0m, (beer.Id, 3));
        await SellAsync(Sep10Afternoon, PaymentMethod.CREDIT_CARD, 0m, (beer.Id, 1));

        var report = await CreateService().GetSalesAsync(new SalesReportQuery { From = Sep01, To = Sep30 });

        Assert.Equal(
            [new PaymentMethodSalesDto(PaymentMethod.PIX, 1, 30m, 75m), new PaymentMethodSalesDto(PaymentMethod.CREDIT_CARD, 1, 10m, 25m)],
            report.PaymentMethods);
    }

    [Fact]
    public async Task Sales_WithoutDates_UsesTheLast30Days()
    {
        var report = await CreateService().GetSalesAsync(new SalesReportQuery());

        Assert.Equal(BusinessClock.Today, report.To);
        Assert.Equal(BusinessClock.Today.AddDays(-29), report.From);
        Assert.Equal(30, report.Periods.Count);
        Assert.Equal(0, report.Summary.SalesCount);
    }

    [Fact]
    public async Task Sales_WithFromAfterTo_ThrowsValidationException()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().GetSalesAsync(new SalesReportQuery { From = Sep30, To = Sep01 }));

        Assert.Contains(exception.Errors, e => e.ErrorMessage == "A data final deve ser igual ou posterior à data inicial.");
    }

    [Fact]
    public async Task Sales_PeriodLongerThanTwoYears_ThrowsValidationException()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().GetSalesAsync(new SalesReportQuery { From = new(2024, 1, 1), To = Sep30 }));

        Assert.Contains(exception.Errors, e => e.ErrorMessage == "O período pode ter no máximo 2 anos.");
    }

    // ---------- Produtos e categorias ----------

    [Fact]
    public async Task ProductRanking_SplitsTheSaleDiscountProportionally()
    {
        var beer = await ProductWithStockAsync("Heineken", cost: 4m, price: 10m);
        var ice = await ProductWithStockAsync("Gelo", cost: 2m, price: 5m);
        // Subtotal 30 + 10 = 40, desconto 4 (10%): Heineken fica com 27 e o gelo com 9.
        await SellAsync(Sep10Afternoon, PaymentMethod.PIX, 4m, (beer.Id, 3), (ice.Id, 2));

        var report = await CreateService().GetProductRankingAsync(new ProductRankingQuery { From = Sep01, To = Sep30 });

        Assert.Equal(
            [
                new ProductSalesDto(beer.Id, "Heineken", "Cerveja", 3, 27m, 12m, 15m, 55.56m),
                new ProductSalesDto(ice.Id, "Gelo", "Cerveja", 2, 9m, 4m, 5m, 55.56m),
            ],
            report.Items);
        Assert.Equal(36m, report.Items.Sum(p => p.Revenue)); // bate com o total da venda
    }

    [Fact]
    public async Task ProductRanking_LeastSold_OnlyAmongProductsThatSold()
    {
        var beer = await ProductWithStockAsync("Heineken", cost: 4m, price: 10m);
        var wine = await ProductWithStockAsync("Malbec", cost: 30m, price: 60m);
        await ProductWithStockAsync("Gin sem venda", cost: 50m, price: 90m);
        await SellAsync(Sep10Afternoon, PaymentMethod.PIX, 0m, (beer.Id, 5), (wine.Id, 1));

        var most = await CreateService().GetProductRankingAsync(new ProductRankingQuery { From = Sep01, To = Sep30 });
        var least = await CreateService().GetProductRankingAsync(new ProductRankingQuery { From = Sep01, To = Sep30, Order = ProductRankingOrder.LeastSold });

        Assert.Equal(["Heineken", "Malbec"], most.Items.Select(p => p.ProductName));
        Assert.Equal(["Malbec", "Heineken"], least.Items.Select(p => p.ProductName));
    }

    [Fact]
    public async Task ProductRanking_RespectsTheLimit()
    {
        var beer = await ProductWithStockAsync("Heineken", cost: 4m, price: 10m);
        var wine = await ProductWithStockAsync("Malbec", cost: 30m, price: 60m);
        await SellAsync(Sep10Afternoon, PaymentMethod.PIX, 0m, (beer.Id, 5), (wine.Id, 1));

        var report = await CreateService().GetProductRankingAsync(new ProductRankingQuery { From = Sep01, To = Sep30, Limit = 1 });

        Assert.Equal("Heineken", Assert.Single(report.Items).ProductName);
    }

    [Fact]
    public async Task Categories_WithShareOfRevenue()
    {
        var beer = await ProductWithStockAsync("Heineken", cost: 4m, price: 10m);
        var wine = await ProductWithStockAsync("Malbec", cost: 30m, price: 60m, categoryId: TestData.VinhoCategoryId);
        await SellAsync(Sep10Afternoon, PaymentMethod.PIX, 0m, (beer.Id, 3), (wine.Id, 1));

        var report = await CreateService().GetCategoriesAsync(new ReportPeriodQuery { From = Sep01, To = Sep30 });

        Assert.Equal(
            [
                new CategorySalesDto(TestData.VinhoCategoryId, "Vinho", 1, 60m, 30m, 66.67m),
                new CategorySalesDto(TestData.CervejaCategoryId, "Cerveja", 3, 30m, 18m, 33.33m),
            ],
            report.Items);
    }

    // ---------- Produtos parados ----------

    private async Task SetCreatedAtAsync(int productId, DateTime createdAt)
    {
        await using var context = _database.CreateContext();
        var product = await context.Products.FindAsync([productId], TestContext.Current.CancellationToken);
        product!.CreatedAt = createdAt;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task IdleProducts_ListsProductsWithStockAndNoRecentSale()
    {
        var oldSale = await ProductWithStockAsync("Vendido há 40 dias", cost: 4m, price: 10m, stock: 10);
        await SellAsync(DateTime.UtcNow.AddDays(-40), PaymentMethod.PIX, 0m, (oldSale.Id, 1));
        var recent = await ProductWithStockAsync("Vendido hoje", cost: 4m, price: 10m, stock: 10);
        await SellAsync(DateTime.UtcNow, PaymentMethod.PIX, 0m, (recent.Id, 1));
        var neverSold = await ProductWithStockAsync("Nunca vendido, antigo", cost: 20m, price: 40m, stock: 3);
        await SetCreatedAtAsync(neverSold.Id, DateTime.UtcNow.AddDays(-100));
        await ProductWithStockAsync("Nunca vendido, chegou agora", cost: 4m, price: 10m, stock: 10);
        var noStock = await ProductWithStockAsync("Sem estoque", cost: 4m, price: 10m, stock: 0);
        await SetCreatedAtAsync(noStock.Id, DateTime.UtcNow.AddDays(-100));

        var thirty = await CreateService().GetIdleProductsAsync(new IdleProductsQuery { Days = 30 });
        var sixty = await CreateService().GetIdleProductsAsync(new IdleProductsQuery { Days = 60 });

        Assert.Equal(["Nunca vendido, antigo", "Vendido há 40 dias"], thirty.Select(p => p.ProductName));
        var never = thirty[0];
        Assert.Null(never.LastSaleDate);
        Assert.Equal(60m, never.StockCostValue); // 3 × R$ 20,00 parados
        Assert.InRange(thirty[1].DaysWithoutSale, 39, 41);
        Assert.NotNull(thirty[1].LastSaleDate);
        Assert.Equal(["Nunca vendido, antigo"], sixty.Select(p => p.ProductName));
    }

    [Fact]
    public async Task IdleProducts_CanceledSaleDoesNotCountAsSale()
    {
        var product = await ProductWithStockAsync("Só venda cancelada", cost: 4m, price: 10m, stock: 10);
        await SetCreatedAtAsync(product.Id, DateTime.UtcNow.AddDays(-90));
        var saleId = await SellAsync(DateTime.UtcNow, PaymentMethod.PIX, 0m, (product.Id, 1));
        await CancelAsync(saleId);

        var report = await CreateService().GetIdleProductsAsync(new IdleProductsQuery { Days = 30 });

        Assert.Null(Assert.Single(report).LastSaleDate);
    }

    [Fact]
    public async Task IdleProducts_IncludesProductWithStockThatWasNeverSold()
    {
        // Nenhuma venda no banco: o produto conta como parado desde o dia do cadastro.
        var product = await ProductWithStockAsync("Licor nunca vendido", cost: 25m, price: 50m, stock: 4);
        await SetCreatedAtAsync(product.Id, DateTime.UtcNow.AddDays(-45));

        var report = await CreateService().GetIdleProductsAsync(new IdleProductsQuery { Days = 30 });

        var idle = Assert.Single(report);
        Assert.Equal(product.Id, idle.ProductId);
        Assert.Null(idle.LastSaleDate);
        Assert.InRange(idle.DaysWithoutSale, 44, 46);
        Assert.Equal(100m, idle.StockCostValue); // 4 × R$ 25,00 parados
    }

    // ---------- Margem ----------

    [Fact]
    public async Task Margins_OnlyBelowMinimum_ReturnsOnlyProductsBelowTheMinimum()
    {
        await TestData.AddProductAsync(_database, "Margem baixa", costPrice: 9m, salePrice: 10m);     // 10%
        await TestData.AddProductAsync(_database, "Margem bem baixa", costPrice: 8m, salePrice: 8.50m); // 5,88%
        await TestData.AddProductAsync(_database, "No mínimo", costPrice: 8.50m, salePrice: 10m);      // 15%: não está abaixo
        await TestData.AddProductAsync(_database, "Margem boa", costPrice: 4m, salePrice: 10m);        // 60%

        var report = await CreateService().GetMarginsAsync(new MarginReportQuery { OnlyBelowMinimum = true });

        // Mínimo padrão de 15%. Ordem: menor margem primeiro.
        Assert.Equal(15m, report.MinimumMarginPercent);
        Assert.Equal(2, report.BelowMinimumCount);
        Assert.Equal(["Margem bem baixa", "Margem baixa"], report.Items.Select(p => p.ProductName));
        Assert.All(report.Items, p => Assert.True(p.BelowMinimum));
    }

    [Fact]
    public async Task Margins_FlagsProductsBelowTheConfiguredMinimum()
    {
        await TestData.AddProductAsync(_database, "Margem baixa", costPrice: 8m, salePrice: 8.50m);   // 5,88%
        await TestData.AddProductAsync(_database, "Margem boa", costPrice: 4m, salePrice: 10m);       // 60%
        await TestData.AddProductAsync(_database, "Inativo", costPrice: 9m, salePrice: 9.10m, active: false);

        var report = await CreateService().GetMarginsAsync(new MarginReportQuery());
        var onlyBelow = await CreateService().GetMarginsAsync(new MarginReportQuery { OnlyBelowMinimum = true });

        Assert.Equal(15m, report.MinimumMarginPercent);
        Assert.Equal(1, report.BelowMinimumCount);
        Assert.Equal(["Margem baixa", "Margem boa"], report.Items.Select(p => p.ProductName));
        Assert.Equal(5.88m, report.Items[0].MarginPercent);
        Assert.True(report.Items[0].BelowMinimum);
        Assert.False(report.Items[1].BelowMinimum);
        Assert.Equal("Margem baixa", Assert.Single(onlyBelow.Items).ProductName);
    }

    [Fact]
    public async Task Margins_UseTheUpdatedMinimum()
    {
        await TestData.AddProductAsync(_database, "Margem boa", costPrice: 4m, salePrice: 10m); // 60%
        await TestData.CreateSettingsService(_database.CreateContext()).UpdateMarginAsync(new MarginSettingsDto(70m));

        var report = await CreateService().GetMarginsAsync(new MarginReportQuery());

        Assert.Equal(70m, report.MinimumMarginPercent);
        Assert.True(Assert.Single(report.Items).BelowMinimum);
    }

    // ---------- Compras por fornecedor ----------

    [Fact]
    public async Task PurchasesBySupplier_SumsPurchasesInThePeriod()
    {
        var first = await TestData.AddSupplierAsync(_database, "Distribuidora A");
        var second = await TestData.AddSupplierAsync(_database, "Distribuidora B");
        await using (var context = _database.CreateContext())
        {
            context.Purchases.AddRange(
                new Purchase { SupplierId = first.Id, UserId = TestData.AdminUserId, PurchaseDate = new(2026, 9, 5), TotalAmount = 100m },
                new Purchase { SupplierId = first.Id, UserId = TestData.AdminUserId, PurchaseDate = new(2026, 9, 20), TotalAmount = 50m },
                new Purchase { SupplierId = second.Id, UserId = TestData.AdminUserId, PurchaseDate = new(2026, 9, 10), TotalAmount = 200m },
                new Purchase { SupplierId = second.Id, UserId = TestData.AdminUserId, PurchaseDate = new(2026, 8, 31), TotalAmount = 999m }); // fora do período
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var report = await CreateService().GetPurchasesBySupplierAsync(new ReportPeriodQuery { From = Sep01, To = Sep30 });

        Assert.Equal(
            [
                new SupplierPurchasesDto(second.Id, "Distribuidora B", 1, 200m, new(2026, 9, 10), 57.14m),
                new SupplierPurchasesDto(first.Id, "Distribuidora A", 2, 150m, new(2026, 9, 20), 42.86m),
            ],
            report.Items);
    }

    // ---------- Estoque ----------

    [Fact]
    public async Task Stock_ValuesStockAtCostAndSalePrice_ByCategory()
    {
        await ProductWithStockAsync("Heineken", cost: 4m, price: 10m, stock: 10);
        await ProductWithStockAsync("Malbec", cost: 30m, price: 60m, stock: 5, categoryId: TestData.VinhoCategoryId);
        var inactive = await ProductWithStockAsync("Inativo", cost: 1m, price: 2m, stock: 7);
        await using (var context = _database.CreateContext())
        {
            (await context.Products.FindAsync([inactive.Id], TestContext.Current.CancellationToken))!.Active = false;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var report = await CreateService().GetStockAsync();

        Assert.Equal(2, report.ProductCount);
        Assert.Equal(15, report.Units);
        Assert.Equal(190m, report.CostValue);
        Assert.Equal(400m, report.SaleValue);
        Assert.Equal(
            [
                new StockCategoryDto(TestData.VinhoCategoryId, "Vinho", 1, 5, 150m, 300m),
                new StockCategoryDto(TestData.CervejaCategoryId, "Cerveja", 1, 10, 40m, 100m),
            ],
            report.Categories);
    }

    public void Dispose() => _database.Dispose();
}
