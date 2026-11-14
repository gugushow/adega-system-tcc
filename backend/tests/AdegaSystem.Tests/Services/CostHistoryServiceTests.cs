using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Purchases;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using AdegaSystem.Tests.Infrastructure;

namespace AdegaSystem.Tests.Services;

public class CostHistoryServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private async Task BuyAsync(int supplierId, int productId, DateOnly date, decimal unitCost)
    {
        await using var context = _database.CreateContext();
        var currentUser = new TestData.FakeCurrentUser();
        var stockService = new StockService(context, currentUser, TestData.CreateAuditService(context), new StockAdjustmentDtoValidator());
        await new PurchaseService(context, currentUser, stockService, new CreatePurchaseDtoValidator())
            .CreateAsync(new CreatePurchaseDto(supplierId, null, date, [new CreatePurchaseItemDto(productId, 12, unitCost)]));
    }

    [Fact]
    public async Task GetByProduct_ReturnsEntriesInDateOrderWithVariation()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);
        // Lançadas fora de ordem de data, para garantir que a ordenação é pela data da compra.
        await BuyAsync(supplier.Id, product.Id, new DateOnly(2026, 8, 1), 4.10m);
        await BuyAsync(supplier.Id, product.Id, new DateOnly(2026, 7, 1), 3.90m);
        await BuyAsync(supplier.Id, product.Id, new DateOnly(2026, 9, 1), 4.35m);

        var history = await new CostHistoryService(_database.CreateContext()).GetByProductAsync(product.Id);

        Assert.Equal([3.90m, 4.10m, 4.35m], history.Entries.Select(e => e.CostPrice));
        Assert.Equal([null, 5.13m, 6.10m], history.Entries.Select(e => e.ChangePercent));
        // (4,35 - 3,90) / 3,90 = 11,54%
        Assert.Equal(11.54m, history.TotalChangePercent);
        Assert.Equal("Distribuidora Boa Bebida", history.Entries[0].SupplierName);
    }

    [Fact]
    public async Task GetByProduct_WithoutPurchases_ReturnsEmpty()
    {
        var product = await TestData.AddProductAsync(_database);

        var history = await new CostHistoryService(_database.CreateContext()).GetByProductAsync(product.Id);

        Assert.Empty(history.Entries);
        Assert.Null(history.TotalChangePercent);
    }

    [Fact]
    public async Task GetByProduct_MissingProduct_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => new CostHistoryService(_database.CreateContext()).GetByProductAsync(999));
    }

    public void Dispose() => _database.Dispose();
}
