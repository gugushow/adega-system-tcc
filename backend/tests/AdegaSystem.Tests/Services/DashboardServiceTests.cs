using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Tests.Infrastructure;

namespace AdegaSystem.Tests.Services;

public class DashboardServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private DashboardService CreateService()
    {
        var context = _database.CreateContext();
        return new DashboardService(context, TestData.CreateReportService(context));
    }

    private async Task SellAsync(int productId, int quantity)
    {
        await using var context = _database.CreateContext();
        await TestData.CreateSaleService(context).CreateAsync(new CreateSaleDto(
            null, PaymentMethod.PIX, 0m, [new CreateSaleItemDto(productId, quantity)]));
    }

    [Fact]
    public async Task Cards_CountTodaySalesStockAndExpiration()
    {
        var today = BusinessClock.Today;

        // Estoque mínimo 10 em todos (padrão do TestData).
        var beer = await TestData.AddProductAsync(_database, "Heineken", costPrice: 4m, salePrice: 10m);
        await TestData.ReceiveBatchAsync(_database, beer.Id, 5, unitCost: 4m);                          // baixo
        await TestData.AddProductAsync(_database, "Sem estoque");                                       // baixo e zerado
        var wine = await TestData.AddProductAsync(_database, "Malbec");
        await TestData.ReceiveBatchAsync(_database, wine.Id, 50, today.AddDays(10));                     // vencendo
        var gin = await TestData.AddProductAsync(_database, "Gin");
        await TestData.ReceiveBatchAsync(_database, gin.Id, 20, today.AddDays(-1), batchNumber: "L-1"); // vencido
        await TestData.ReceiveBatchAsync(_database, gin.Id, 20, today.AddDays(-2), batchNumber: "L-2"); // vencido (mesmo produto)

        await SellAsync(beer.Id, 2); // hoje: R$ 20,00, custo R$ 8,00

        var dashboard = await CreateService().GetAsync();
        var cards = dashboard.Cards;

        Assert.Equal(1, cards.SalesToday);
        Assert.Equal(20m, cards.RevenueToday);
        Assert.Equal(20m, cards.RevenueMonth);
        Assert.Equal(12m, cards.ProfitMonth);
        Assert.Equal(4, cards.ActiveProducts);
        Assert.Equal(3 + 50 + 40, cards.TotalItemsInStock);
        Assert.Equal(2, cards.LowStockProducts);
        Assert.Equal(1, cards.OutOfStockProducts);
        Assert.Equal(1, cards.ExpiringProducts);
        Assert.Equal(1, cards.ExpiredProducts);
        Assert.Equal(30, cards.ExpiringDays);
    }

    [Fact]
    public async Task Charts_HaveOnePointPerDayAndPerMonth()
    {
        var beer = await TestData.AddProductAsync(_database, "Heineken", costPrice: 4m, salePrice: 10m);
        await TestData.ReceiveBatchAsync(_database, beer.Id, 10, unitCost: 4m);
        await SellAsync(beer.Id, 3);

        var dashboard = await CreateService().GetAsync();

        Assert.Equal(30, dashboard.Last30Days.Count);
        Assert.Equal(BusinessClock.Today, dashboard.Last30Days[^1].Period);
        Assert.Equal(30m, dashboard.Last30Days[^1].Revenue);
        Assert.Equal(12, dashboard.Last12Months.Count);
        Assert.Equal(BusinessClock.FirstDayOfMonth(BusinessClock.Today), dashboard.Last12Months[^1].Period);
        Assert.Equal("Heineken", Assert.Single(dashboard.TopProducts).ProductName);
        Assert.Equal("Cerveja", Assert.Single(dashboard.TopCategories).CategoryName);
        Assert.Equal(PaymentMethod.PIX, Assert.Single(dashboard.PaymentMethodsMonth).PaymentMethod);
    }

    public void Dispose() => _database.Dispose();
}
