using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Batches;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Services;

public class BatchServiceTests : IDisposable
{
    // As datas são relativas a "hoje" (horário de Brasília), para o teste valer em qualquer dia.
    private static readonly DateOnly Today = BusinessClock.Today;

    private readonly TestDatabase _database = new();

    private BatchService CreateService()
    {
        var context = _database.CreateContext();
        return new BatchService(context, TestData.CreateStockService(context), TestData.CreateAuditService(context));
    }

    [Fact]
    public async Task GetAll_OrdersByExpirationWithNoExpiryLast()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, null, "SEM-VALIDADE");
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(90), "B");
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(10), "A");

        var result = await CreateService().GetAllAsync(new BatchQuery { ProductId = product.Id });

        Assert.Equal(["A", "B", "SEM-VALIDADE"], result.Items.Select(b => b.BatchNumber));
        Assert.Equal(10, result.Items[0].DaysToExpire);
        Assert.Null(result.Items[2].DaysToExpire);
    }

    [Fact]
    public async Task GetAll_HidesEmptyBatchesUnlessAsked()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 2, Today.AddDays(-1), "VAZIO");
        await CreateService().WriteOffAsync(batch.Id, new WriteOffBatchDto(null));

        var available = await CreateService().GetAllAsync(new BatchQuery { ProductId = product.Id });
        var all = await CreateService().GetAllAsync(new BatchQuery { ProductId = product.Id, OnlyAvailable = false });

        Assert.Empty(available.Items);
        Assert.Single(all.Items);
    }

    [Fact]
    public async Task Expired_ReturnsOnlyBatchesPastExpirationWithStock()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(-1), "VENCEU-ONTEM");
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today, "VENCE-HOJE");
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, null, "SEM-VALIDADE");

        var result = await CreateService().GetAllAsync(new BatchQuery { Status = BatchStatusFilter.Expired });

        var batch = Assert.Single(result.Items);
        Assert.Equal("VENCEU-ONTEM", batch.BatchNumber);
        Assert.True(batch.IsExpired);
    }

    [Fact]
    public async Task Expiring_ReturnsBatchesFromTodayUntilTheLimit()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(-1), "VENCIDO");
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today, "HOJE");
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(7), "SETE-DIAS");
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(8), "OITO-DIAS");
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, null, "SEM-VALIDADE");

        var result = await CreateService().GetAllAsync(new BatchQuery { Status = BatchStatusFilter.Expiring, Days = 7 });

        Assert.Equal(["HOJE", "SETE-DIAS"], result.Items.Select(b => b.BatchNumber));
    }

    [Fact]
    public async Task WriteOff_ExpiredBatch_ZeroesBatchAndRecordsExpiredMovement()
    {
        var product = await TestData.AddProductAsync(_database);
        var expired = await TestData.ReceiveBatchAsync(_database, product.Id, 8, Today.AddDays(-3), "A");
        await TestData.ReceiveBatchAsync(_database, product.Id, 20, Today.AddDays(60), "B");

        var result = await CreateService().WriteOffAsync(expired.Id, new WriteOffBatchDto(null));

        Assert.Equal(0, result.QuantityAvailable);
        Assert.Equal(0, await TestData.GetBatchQuantityAsync(_database, expired.Id));
        Assert.Equal(20, await TestData.GetStockAsync(_database, product.Id));
        await TestData.AssertStockMatchesBatchesAsync(_database, product.Id);

        await using var context = _database.CreateContext();
        var movement = await context.StockMovements.OrderBy(m => m.Id).LastAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MovementType.EXPIRED, movement.MovementType);
        Assert.Equal(-8, movement.Quantity);
        Assert.Equal(expired.Id, movement.BatchId);
        Assert.StartsWith("Lote A vencido em", movement.Reason);
    }

    [Fact]
    public async Task WriteOff_WithoutReason_UsesTheDefaultReason()
    {
        var product = await TestData.AddProductAsync(_database);
        var expiration = Today.AddDays(-5);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 6, expiration, "L-2026-07");

        // Motivo só com espaços conta como "sem motivo".
        await CreateService().WriteOffAsync(batch.Id, new WriteOffBatchDto("   "));

        // Data montada à mão (dd/MM/yyyy), sem depender da cultura da máquina.
        var date = $"{expiration.Day:00}/{expiration.Month:00}/{expiration.Year}";
        await using var context = _database.CreateContext();
        var movement = await context.StockMovements.SingleAsync(m => m.MovementType == MovementType.EXPIRED, TestContext.Current.CancellationToken);
        Assert.Equal($"Lote L-2026-07 vencido em {date}", movement.Reason);
    }

    [Fact]
    public async Task WriteOff_WithReasonLongerThan255Chars_ThrowsAndKeepsTheStock()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 6, Today.AddDays(-5));

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().WriteOffAsync(batch.Id, new WriteOffBatchDto(new string('x', 256))));

        Assert.Equal("O motivo deve ter no máximo 255 caracteres.", exception.Message);
        Assert.Equal(6, await TestData.GetBatchQuantityAsync(_database, batch.Id));
        Assert.Equal(6, await TestData.GetStockAsync(_database, product.Id));
    }

    [Fact]
    public async Task WriteOff_BatchNotExpired_Throws()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 8, Today, "VENCE-HOJE");

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().WriteOffAsync(batch.Id, new WriteOffBatchDto(null)));

        Assert.Equal("Só é possível dar baixa em lotes vencidos. Para outras saídas, use o ajuste de estoque.", exception.Message);
        Assert.Equal(8, await TestData.GetStockAsync(_database, product.Id));
    }

    [Fact]
    public async Task WriteOff_EmptyBatch_Throws()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 8, Today.AddDays(-3));
        await CreateService().WriteOffAsync(batch.Id, new WriteOffBatchDto(null));

        await Assert.ThrowsAsync<BusinessException>(() => CreateService().WriteOffAsync(batch.Id, new WriteOffBatchDto(null)));
    }

    [Fact]
    public async Task WriteOff_MissingBatch_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().WriteOffAsync(999, new WriteOffBatchDto(null)));
    }

    public void Dispose() => _database.Dispose();
}
