using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Purchases;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Services;

public class PurchaseServiceTests : IDisposable
{
    private static readonly DateOnly PurchaseDate = new(2026, 9, 30);

    private readonly TestDatabase _database = new();

    /// <summary>PurchaseService e StockService compartilham o mesmo DbContext, como acontece numa requisição HTTP.</summary>
    private static PurchaseService CreateService(AdegaDbContext context)
    {
        var currentUser = new TestData.FakeCurrentUser();
        var stockService = new StockService(context, currentUser, TestData.CreateAuditService(context), new StockAdjustmentDtoValidator());
        return new PurchaseService(context, currentUser, stockService, new CreatePurchaseDtoValidator());
    }

    private async Task<PurchaseDetailsDto> CreatePurchaseAsync(CreatePurchaseDto dto)
    {
        await using var context = _database.CreateContext();
        return await CreateService(context).CreateAsync(dto);
    }

    private static CreatePurchaseDto NewPurchase(int supplierId, params CreatePurchaseItemDto[] items) =>
        new(supplierId, "NF-1001", PurchaseDate, items);

    [Fact]
    public async Task Create_CalculatesItemTotalsAndPurchaseTotal()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var heineken = await TestData.AddProductAsync(_database);
        var vinho = await TestData.AddProductAsync(_database, name: "Vinho Tinto");

        var purchase = await CreatePurchaseAsync(NewPurchase(supplier.Id,
            new CreatePurchaseItemDto(heineken.Id, 12, 3.8975m),
            new CreatePurchaseItemDto(vinho.Id, 6, 10.00m)));

        // 12 x 3,8975 = 46,77 (arredondado em 2 casas) e 6 x 10,00 = 60,00
        Assert.Equal([46.77m, 60.00m], purchase.Items.Select(i => i.Total));
        Assert.Equal(106.77m, purchase.TotalAmount);
        Assert.Equal("Distribuidora Boa Bebida", purchase.SupplierName);
        Assert.Equal("Administrador", purchase.UserName);
    }

    [Fact]
    public async Task Create_IncreasesStockAndRecordsPurchaseMovements()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        var purchase = await CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 24, 3.90m)));

        Assert.Equal(24, await TestData.GetStockAsync(_database, product.Id));

        await using var context = _database.CreateContext();
        var movement = await context.StockMovements.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MovementType.PURCHASE, movement.MovementType);
        Assert.Equal(24, movement.Quantity);
        Assert.Equal("Purchase", movement.ReferenceType);
        Assert.Equal(purchase.Id, movement.ReferenceId);
        Assert.Equal($"Compra #{purchase.Id}", movement.Reason);
    }

    [Fact]
    public async Task Create_UpdatesProductCostAndRecordsPriceHistory()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database, costPrice: 3.90m);

        var purchase = await CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 12, 4.10m)));

        await using var context = _database.CreateContext();
        var updated = await context.Products.FindAsync([product.Id], TestContext.Current.CancellationToken);
        Assert.Equal(4.10m, updated!.CostPrice);
        Assert.NotNull(updated.UpdatedAt);

        var history = await context.ProductPriceHistories.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(product.Id, history.ProductId);
        Assert.Equal(supplier.Id, history.SupplierId);
        Assert.Equal(purchase.Id, history.PurchaseId);
        Assert.Equal(4.10m, history.CostPrice);
        Assert.Equal(PurchaseDate, history.Date);
    }

    [Fact]
    public async Task Create_BackdatedPurchase_KeepsNewerCostButRecordsHistory()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database, costPrice: 3.90m);
        await CreatePurchaseAsync(new CreatePurchaseDto(supplier.Id, "NF-2", PurchaseDate, [new CreatePurchaseItemDto(product.Id, 12, 4.35m)]));

        // Nota antiga lançada depois: entra no histórico, mas o custo atual continua o da compra mais recente.
        await CreatePurchaseAsync(new CreatePurchaseDto(supplier.Id, "NF-1", PurchaseDate.AddDays(-20), [new CreatePurchaseItemDto(product.Id, 12, 4.10m)]));

        await using var context = _database.CreateContext();
        var updated = await context.Products.FindAsync([product.Id], TestContext.Current.CancellationToken);
        Assert.Equal(4.35m, updated!.CostPrice);
        Assert.Equal(2, await context.ProductPriceHistories.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_WithFutureDate_ThrowsValidationException()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        await Assert.ThrowsAsync<ValidationException>(() => CreatePurchaseAsync(new CreatePurchaseDto(
            supplier.Id, null, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2), [new CreatePurchaseItemDto(product.Id, 1, 1m)])));
    }

    [Fact]
    public async Task Create_WithInactiveProduct_ThrowsAndSavesNothing()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var active = await TestData.AddProductAsync(_database);
        var inactive = await TestData.AddProductAsync(_database, name: "Fora de linha", active: false);

        await Assert.ThrowsAsync<BusinessException>(() => CreatePurchaseAsync(NewPurchase(supplier.Id,
            new CreatePurchaseItemDto(active.Id, 10, 3.90m),
            new CreatePurchaseItemDto(inactive.Id, 10, 3.90m))));

        await using var context = _database.CreateContext();
        Assert.Equal(0, await context.Purchases.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.StockMovements.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await TestData.GetStockAsync(_database, active.Id));
    }

    [Fact]
    public async Task Create_WithInactiveSupplier_Throws()
    {
        var supplier = await TestData.AddSupplierAsync(_database, active: false);
        var product = await TestData.AddProductAsync(_database);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 1, 1m))));
        Assert.Equal("O fornecedor informado está inativo.", exception.Message);
    }

    [Fact]
    public async Task Create_SameProductInTwoBatches_CreatesOneBatchPerItem()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        var purchase = await CreatePurchaseAsync(NewPurchase(supplier.Id,
            new CreatePurchaseItemDto(product.Id, 30, 3.90m, "A", null, new DateOnly(2026, 11, 10)),
            new CreatePurchaseItemDto(product.Id, 50, 3.90m, "B", new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 20))));

        Assert.Equal(80, await TestData.GetStockAsync(_database, product.Id));
        Assert.Equal(["A", "B"], purchase.Items.Select(i => i.BatchNumber));
        Assert.Equal(new DateOnly(2027, 1, 20), purchase.Items[1].ExpirationDate);

        await using var context = _database.CreateContext();
        var batches = await context.ProductBatches.OrderBy(b => b.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([30, 50], batches.Select(b => b.QuantityAvailable));
        Assert.All(batches, b => Assert.Equal(purchase.Id, b.PurchaseId));
        Assert.Equal(new DateOnly(2026, 9, 1), batches[1].ManufacturingDate);
        Assert.Equal(purchase.Items.Select(i => i.BatchId), batches.Select(b => (int?)b.Id));
        await TestData.AssertStockMatchesBatchesAsync(_database, product.Id);
    }

    [Fact]
    public async Task Create_WithoutBatchNumber_GeneratesOneFromThePurchase()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        var purchase = await CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 10, 1m, "  ")));

        Assert.Equal($"COMPRA-{purchase.Id}", purchase.Items[0].BatchNumber);
    }

    [Fact]
    public async Task Create_WithSameProductAndBatchTwice_ThrowsValidationException()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        await Assert.ThrowsAsync<ValidationException>(() => CreatePurchaseAsync(NewPurchase(supplier.Id,
            new CreatePurchaseItemDto(product.Id, 1, 1m, "a1"),
            new CreatePurchaseItemDto(product.Id, 2, 1m, " A1 "))));
    }

    [Fact]
    public async Task Create_WithExpirationBeforeManufacturing_ThrowsValidationException()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreatePurchaseAsync(NewPurchase(supplier.Id,
            new CreatePurchaseItemDto(product.Id, 1, 1m, "A", new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 1)))));

        Assert.Contains(exception.Errors, e => e.PropertyName == "Items[0].ExpirationDate");
    }

    [Fact]
    public async Task Create_WithoutItemsOrWithZeroQuantity_ThrowsValidationException()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        await Assert.ThrowsAsync<ValidationException>(() => CreatePurchaseAsync(NewPurchase(supplier.Id)));
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 0, 1m))));
    }

    [Fact]
    public async Task Create_WithRepeatedInvoiceForSameSupplier_Throws()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var other = await TestData.AddSupplierAsync(_database, name: "Outro fornecedor");
        var product = await TestData.AddProductAsync(_database);
        await CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 1, 1m)));

        // A mesma nota de outro fornecedor é permitida.
        await CreatePurchaseAsync(NewPurchase(other.Id, new CreatePurchaseItemDto(product.Id, 1, 1m)));

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 1, 1m))));
        Assert.Equal("Já existe uma compra com essa nota fiscal para este fornecedor.", exception.Message);
    }

    [Fact]
    public async Task Create_InvoiceWithSurroundingSpaces_CountsAsTheSameInvoice()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);
        var first = await CreatePurchaseAsync(new CreatePurchaseDto(supplier.Id, "NF-1001", PurchaseDate, [new CreatePurchaseItemDto(product.Id, 1, 1m)]));

        // " NF-1001 " é gravado sem os espaços, então é a mesma nota do mesmo fornecedor.
        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreatePurchaseAsync(
            new CreatePurchaseDto(supplier.Id, "  NF-1001  ", PurchaseDate, [new CreatePurchaseItemDto(product.Id, 1, 1m)])));

        Assert.Equal("Já existe uma compra com essa nota fiscal para este fornecedor.", exception.Message);
        Assert.Equal("NF-1001", first.InvoiceNumber);
    }

    [Fact]
    public async Task Create_SameInvoiceForDifferentSupplier_IsAccepted()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var other = await TestData.AddSupplierAsync(_database, name: "Outro fornecedor");
        var product = await TestData.AddProductAsync(_database);
        await CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 1, 1m)));

        // Fornecedores diferentes podem usar a mesma numeração de nota.
        var second = await CreatePurchaseAsync(NewPurchase(other.Id, new CreatePurchaseItemDto(product.Id, 1, 1m)));

        Assert.Equal("NF-1001", second.InvoiceNumber);
        Assert.Equal(other.Id, second.SupplierId);
        await using var context = _database.CreateContext();
        Assert.Equal(2, await context.Purchases.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_WithUnknownProduct_ThrowsAndSavesNothing()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreatePurchaseAsync(NewPurchase(supplier.Id,
            new CreatePurchaseItemDto(product.Id, 10, 3.90m),
            new CreatePurchaseItemDto(9999, 10, 3.90m))));

        Assert.Equal("O produto de código 9999 não existe.", exception.Message);

        // Nada foi gravado: nem a compra, nem movimentação, nem lote, nem estoque.
        await using var context = _database.CreateContext();
        Assert.Equal(0, await context.Purchases.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.StockMovements.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.ProductBatches.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await TestData.GetStockAsync(_database, product.Id));
    }

    [Fact]
    public async Task Create_WithUnitCostAboveFourDecimals_ThrowsValidationException()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        // A coluna guarda 4 casas: 3,89751 seria arredondado sem aviso.
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 1, 3.89751m))));

        Assert.Contains(exception.Errors, e =>
            e.PropertyName == "Items[0].UnitCost" && e.ErrorMessage == "O custo unitário aceita no máximo 4 casas decimais.");
    }

    [Fact]
    public async Task Create_WithQuantityAboveTheLimit_ThrowsValidationException()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var product = await TestData.AddProductAsync(_database);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, QuantityRules.Max + 1, 1m))));

        Assert.Contains(exception.Errors, e =>
            e.PropertyName == "Items[0].Quantity" && e.ErrorMessage == "A quantidade deve ser de no máximo 100.000.");
    }

    [Fact]
    public async Task GetAll_FiltersBySupplier()
    {
        var supplier = await TestData.AddSupplierAsync(_database);
        var other = await TestData.AddSupplierAsync(_database, name: "Outro fornecedor");
        var product = await TestData.AddProductAsync(_database);
        await CreatePurchaseAsync(NewPurchase(supplier.Id, new CreatePurchaseItemDto(product.Id, 1, 1m)));
        await CreatePurchaseAsync(NewPurchase(other.Id, new CreatePurchaseItemDto(product.Id, 1, 1m)));

        await using var context = _database.CreateContext();
        var result = await CreateService(context).GetAllAsync(new PurchaseQuery { SupplierId = supplier.Id });

        var purchase = Assert.Single(result.Items);
        Assert.Equal(supplier.Id, purchase.SupplierId);
        Assert.Equal(1, purchase.ItemCount);
    }

    public void Dispose() => _database.Dispose();
}
