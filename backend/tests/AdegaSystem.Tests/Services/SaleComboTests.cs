using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Combos;
using AdegaSystem.Api.DTOs.Reports;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Services;

/// <summary>Venda de combos: o combo vira um item por produto, e cada produto sai dos próprios lotes.</summary>
public class SaleComboTests : IDisposable
{
    private static readonly DateOnly Today = BusinessClock.Today;

    private readonly TestDatabase _database = new();

    private SaleService CreateService() => TestData.CreateSaleService(_database.CreateContext());

    private static CreateSaleDto NewSale(IReadOnlyList<CreateSaleItemDto> items, params CreateSaleComboDto[] combos) =>
        new(null, PaymentMethod.PIX, 0, items, combos);

    private async Task<(Product Whisky, Product Energy, Product Ice, ComboDto Combo)> AddWhiskyComboAsync(
        int whiskyStock = 5, int energyStock = 20, int iceStock = 10)
    {
        var whisky = await TestData.AddProductAsync(_database, "Jack Daniel's 1L", costPrice: 110m, salePrice: 150m);
        var energy = await TestData.AddProductAsync(_database, "Red Bull 250ml", costPrice: 8m, salePrice: 15m);
        var ice = await TestData.AddProductAsync(_database, "Gelo 2kg", costPrice: 4m, salePrice: 10m);
        await TestData.ReceiveBatchAsync(_database, whisky.Id, whiskyStock, Today.AddDays(365), unitCost: 110m);
        await TestData.ReceiveBatchAsync(_database, energy.Id, energyStock, Today.AddDays(90), unitCost: 8m);
        await TestData.ReceiveBatchAsync(_database, ice.Id, iceStock, null, unitCost: 4m);

        var combo = await TestData.CreateComboService(_database.CreateContext()).CreateAsync(new CreateComboDto(
            "Combo Whisky", null, 199m, [new(whisky.Id, 1), new(energy.Id, 4), new(ice.Id, 1)]));
        return (whisky, energy, ice, combo);
    }

    [Fact]
    public async Task Create_WithCombo_OpensOneItemPerProduct_WithTheSavingsSplitAsDiscount()
    {
        var (whisky, energy, ice, combo) = await AddWhiskyComboAsync();

        var sale = await CreateService().CreateAsync(NewSale([], new CreateSaleComboDto(combo.Id, 1)));

        Assert.Equal(199m, sale.Subtotal);
        Assert.Equal(199m, sale.Total);
        Assert.Equal(3, sale.Items.Count);
        Assert.All(sale.Items, i => Assert.Equal(("Combo Whisky", combo.Id), (i.ComboName, i.ComboId!.Value)));

        // Economia de R$ 21,00 dividida pelo peso de cada produto no preço normal (150 + 60 + 10 = 220).
        var whiskyItem = sale.Items.Single(i => i.ProductId == whisky.Id);
        Assert.Equal((1, 150m, 14.32m, 135.68m), (whiskyItem.Quantity, whiskyItem.UnitPrice, whiskyItem.Discount, whiskyItem.Total));
        var energyItem = sale.Items.Single(i => i.ProductId == energy.Id);
        Assert.Equal((4, 15m, 5.73m, 54.27m), (energyItem.Quantity, energyItem.UnitPrice, energyItem.Discount, energyItem.Total));
        var iceItem = sale.Items.Single(i => i.ProductId == ice.Id);
        Assert.Equal((1, 10m, 0.95m, 9.05m), (iceItem.Quantity, iceItem.UnitPrice, iceItem.Discount, iceItem.Total));

        // Custo: 110 + 4 × 8 + 4 = 146.
        Assert.Equal(146m, sale.TotalCost);
        Assert.Equal(53m, sale.Profit);
    }

    [Fact]
    public async Task Create_WithCombo_ReducesTheStockOfEachProduct()
    {
        var (whisky, energy, ice, combo) = await AddWhiskyComboAsync();

        await CreateService().CreateAsync(NewSale([], new CreateSaleComboDto(combo.Id, 2)));

        Assert.Equal(3, await TestData.GetStockAsync(_database, whisky.Id));
        Assert.Equal(12, await TestData.GetStockAsync(_database, energy.Id));
        Assert.Equal(8, await TestData.GetStockAsync(_database, ice.Id));
        await TestData.AssertStockMatchesBatchesAsync(_database, energy.Id);
    }

    [Fact]
    public async Task Create_SameProductLooseAndInCombo_DoesNotSellTheSameUnitTwice()
    {
        // 6 energéticos: o avulso leva 3 e o combo precisa de 4 → só sobram 3.
        var (_, energy, _, combo) = await AddWhiskyComboAsync(energyStock: 6);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService().CreateAsync(
            NewSale([new CreateSaleItemDto(energy.Id, 3)], new CreateSaleComboDto(combo.Id, 1))));

        Assert.Equal("Estoque insuficiente para \"Red Bull 250ml\". Disponível para venda: 3, solicitado: 4.", exception.Message);
        Assert.Equal(6, await TestData.GetStockAsync(_database, energy.Id));
    }

    [Fact]
    public async Task Create_SameProductLooseAndInCombo_WithEnoughStock_UsesBothFromTheSameBatch()
    {
        var (_, energy, _, combo) = await AddWhiskyComboAsync(energyStock: 6);

        var sale = await CreateService().CreateAsync(NewSale([new CreateSaleItemDto(energy.Id, 2)], new CreateSaleComboDto(combo.Id, 1)));

        Assert.Equal(30m + 199m, sale.Subtotal);
        Assert.Equal(0, await TestData.GetStockAsync(_database, energy.Id));
        await TestData.AssertStockMatchesBatchesAsync(_database, energy.Id);
    }

    [Fact]
    public async Task Create_InactiveCombo_Throws()
    {
        var (_, _, _, combo) = await AddWhiskyComboAsync();
        await TestData.CreateComboService(_database.CreateContext()).DeactivateAsync(combo.Id);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateService().CreateAsync(NewSale([], new CreateSaleComboDto(combo.Id, 1))));

        Assert.Equal("O combo \"Combo Whisky\" está inativo e não pode ser vendido.", exception.Message);
    }

    [Fact]
    public async Task Create_ComboMoreExpensiveThanProductsAfterPriceDrop_Throws()
    {
        var (whisky, _, _, combo) = await AddWhiskyComboAsync();
        await using (var context = _database.CreateContext())
        {
            var product = await context.Products.FindAsync([whisky.Id], TestContext.Current.CancellationToken);
            product!.SalePrice = 100m; // 100 + 60 + 10 = 170 < 199
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateService().CreateAsync(NewSale([], new CreateSaleComboDto(combo.Id, 1))));

        Assert.Contains("ficou mais caro que os produtos separados", exception.Message);
    }

    [Fact]
    public async Task Cancel_ComboSale_ReturnsEveryProductToItsBatch()
    {
        var (whisky, energy, ice, combo) = await AddWhiskyComboAsync();
        var sale = await CreateService().CreateAsync(NewSale([], new CreateSaleComboDto(combo.Id, 1)));

        await CreateService().CancelAsync(sale.Id, new CancelSaleDto("Cliente desistiu"));

        Assert.Equal(5, await TestData.GetStockAsync(_database, whisky.Id));
        Assert.Equal(20, await TestData.GetStockAsync(_database, energy.Id));
        Assert.Equal(10, await TestData.GetStockAsync(_database, ice.Id));
    }

    [Fact]
    public async Task Reports_CountComboItemsAsProductSales()
    {
        var (whisky, _, _, combo) = await AddWhiskyComboAsync();
        await CreateService().CreateAsync(NewSale([], new CreateSaleComboDto(combo.Id, 1)));

        var ranking = await TestData.CreateReportService(_database.CreateContext())
            .GetProductRankingAsync(new ProductRankingQuery());

        var row = ranking.Items.Single(p => p.ProductId == whisky.Id);
        Assert.Equal(1, row.Quantity);
        Assert.Equal(135.68m, row.Revenue); // a parte do preço do combo que coube ao whisky
        Assert.Equal(199m, ranking.Items.Sum(p => p.Revenue));
    }

    public void Dispose() => _database.Dispose();
}
