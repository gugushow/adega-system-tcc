using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Entities;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Services;

public class StockServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    /// <summary>Registra uma movimentação do jeito que os outros services fazem: dentro de uma transação.</summary>
    private async Task RegisterInTransactionAsync(StockMovementRequest request)
    {
        await using var context = _database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await TestData.CreateStockService(context).RegisterMovementAsync(request);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReceiveBatch_CreatesBatchIncreasesStockAndRecordsMovement()
    {
        var product = await TestData.AddProductAsync(_database);
        var expiration = new DateOnly(2027, 1, 20);

        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 24, expiration, batchNumber: "A123");

        Assert.Equal(24, batch.QuantityReceived);
        Assert.Equal(24, await TestData.GetBatchQuantityAsync(_database, batch.Id));
        Assert.Equal(24, await TestData.GetStockAsync(_database, product.Id));

        await using var context = _database.CreateContext();
        var movement = await context.StockMovements.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MovementType.PURCHASE, movement.MovementType);
        Assert.Equal(batch.Id, movement.BatchId);
        Assert.Equal(24, movement.Quantity);
        Assert.Equal(0, movement.PreviousStock);
        Assert.Equal(24, movement.NewStock);
        Assert.Equal(TestData.AdminUserId, movement.UserId);
    }

    [Fact]
    public async Task ReceiveBatch_OutsideTransaction_Throws()
    {
        var product = await TestData.AddProductAsync(_database);
        await using var context = _database.CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => TestData.CreateStockService(context)
            .ReceiveBatchAsync(new ReceiveBatchRequest(product.Id, 10, 1m, "A", null, null, null, "Entrada")));
    }

    [Fact]
    public async Task RegisterMovement_OutsideTransaction_Throws()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 10);
        await using var context = _database.CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => TestData.CreateStockService(context)
            .RegisterMovementAsync(new StockMovementRequest(batch.Id, MovementType.LOSS, -1, "Quebra")));
    }

    [Fact]
    public async Task RegisterMovement_Exit_DecreasesBatchAndProductTogether()
    {
        var product = await TestData.AddProductAsync(_database);
        var batchA = await TestData.ReceiveBatchAsync(_database, product.Id, 30, batchNumber: "A");
        var batchB = await TestData.ReceiveBatchAsync(_database, product.Id, 50, batchNumber: "B");

        await RegisterInTransactionAsync(new StockMovementRequest(batchB.Id, MovementType.LOSS, -5, "Quebra"));

        Assert.Equal(30, await TestData.GetBatchQuantityAsync(_database, batchA.Id));
        Assert.Equal(45, await TestData.GetBatchQuantityAsync(_database, batchB.Id));
        Assert.Equal(75, await TestData.GetStockAsync(_database, product.Id));
        await TestData.AssertStockMatchesBatchesAsync(_database, product.Id);

        await using var context = _database.CreateContext();
        var movement = await context.StockMovements.OrderBy(m => m.Id).LastAsync(TestContext.Current.CancellationToken);
        Assert.Equal(batchB.Id, movement.BatchId);
        Assert.Equal(80, movement.PreviousStock);
        Assert.Equal(75, movement.NewStock);
    }

    [Fact]
    public async Task RegisterMovement_MoreThanTheBatchHas_ThrowsEvenIfProductHasMore()
    {
        var product = await TestData.AddProductAsync(_database);
        var batchA = await TestData.ReceiveBatchAsync(_database, product.Id, 5, batchNumber: "A");
        await TestData.ReceiveBatchAsync(_database, product.Id, 50, batchNumber: "B");

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            RegisterInTransactionAsync(new StockMovementRequest(batchA.Id, MovementType.LOSS, -6, "Quebra")));

        Assert.StartsWith("Saldo insuficiente no lote A", exception.Message);
        Assert.Equal(5, await TestData.GetBatchQuantityAsync(_database, batchA.Id));
        Assert.Equal(55, await TestData.GetStockAsync(_database, product.Id));
    }

    [Theory]
    [InlineData(MovementType.LOSS, 1)]
    [InlineData(MovementType.SALE, 1)]
    [InlineData(MovementType.EXPIRED, 1)]
    [InlineData(MovementType.RETURN, -1)]
    [InlineData(MovementType.ADJUSTMENT, 0)]
    public async Task RegisterMovement_WithWrongSign_Throws(MovementType type, int quantity)
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 10);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            RegisterInTransactionAsync(new StockMovementRequest(batch.Id, type, quantity, "Teste")));
    }

    [Fact]
    public async Task RegisterMovement_Purchase_IsOnlyThroughReceiveBatch()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 10);

        // Toda compra cria um lote novo; somar uma compra num lote existente não é permitido.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            RegisterInTransactionAsync(new StockMovementRequest(batch.Id, MovementType.PURCHASE, 5, "Compra")));
    }

    [Fact]
    public async Task Adjust_Loss_DecreasesTheChosenBatch()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 12, batchNumber: "L-77");

        await using var context = _database.CreateContext();
        var movement = await TestData.CreateStockService(context).AdjustAsync(
            new StockAdjustmentDto(batch.Id, MovementType.LOSS, -2, "  Duas garrafas quebradas  "));

        Assert.Equal(12, movement.PreviousStock);
        Assert.Equal(10, movement.NewStock);
        Assert.Equal("Duas garrafas quebradas", movement.Reason);
        Assert.Equal("Heineken 350ml", movement.ProductName);
        Assert.Equal("L-77", movement.BatchNumber);
        Assert.Equal("Administrador", movement.UserName);
        Assert.Equal(10, await TestData.GetBatchQuantityAsync(_database, batch.Id));
        await TestData.AssertStockMatchesBatchesAsync(_database, product.Id);
    }

    [Fact]
    public async Task Adjust_InventoryCount_CanIncreaseABatch()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 10);
        await using var context = _database.CreateContext();

        var movement = await TestData.CreateStockService(context).AdjustAsync(
            new StockAdjustmentDto(batch.Id, MovementType.ADJUSTMENT, 3, "Contagem de inventário"));

        Assert.Equal(13, movement.NewStock);
        Assert.Equal(13, await TestData.GetBatchQuantityAsync(_database, batch.Id));
    }

    [Theory]
    [InlineData(MovementType.LOSS, 2, "Garrafa quebrada")] // perda precisa ser negativa
    [InlineData(MovementType.EXPIRED, -2, "Vencido")]      // vencidos têm a baixa própria
    [InlineData(MovementType.ADJUSTMENT, 2, "")]           // motivo obrigatório
    public async Task Adjust_WithInvalidInput_ThrowsValidationException(MovementType type, int quantity, string reason)
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 10);
        await using var context = _database.CreateContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            TestData.CreateStockService(context).AdjustAsync(new StockAdjustmentDto(batch.Id, type, quantity, reason)));
    }

    [Fact]
    public async Task Adjust_MissingBatch_ThrowsNotFound()
    {
        await using var context = _database.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            TestData.CreateStockService(context).AdjustAsync(new StockAdjustmentDto(999, MovementType.ADJUSTMENT, 1, "Teste")));
    }

    [Fact]
    public async Task GetMovements_FiltersByProductAndType_NewestFirst()
    {
        var heineken = await TestData.AddProductAsync(_database);
        var vinho = await TestData.AddProductAsync(_database, name: "Vinho Tinto");
        var batch = await TestData.ReceiveBatchAsync(_database, heineken.Id, 10);
        await TestData.ReceiveBatchAsync(_database, vinho.Id, 5);
        await RegisterInTransactionAsync(new StockMovementRequest(batch.Id, MovementType.LOSS, -1, "Quebra"));
        await RegisterInTransactionAsync(new StockMovementRequest(batch.Id, MovementType.LOSS, -2, "Quebra"));

        await using var context = _database.CreateContext();
        var result = await TestData.CreateStockService(context).GetMovementsAsync(
            new StockMovementQuery { ProductId = heineken.Id, Type = MovementType.LOSS });

        Assert.Equal(2, result.TotalItems);
        Assert.Equal([-2, -1], result.Items.Select(m => m.Quantity));
        Assert.All(result.Items, m => Assert.Equal("L-TESTE", m.BatchNumber));
    }

    public void Dispose() => _database.Dispose();
}
