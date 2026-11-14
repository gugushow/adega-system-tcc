using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Services;

public class SaleServiceTests : IDisposable
{
    private static readonly DateOnly Today = BusinessClock.Today;

    private readonly TestDatabase _database = new();

    private SaleService CreateService() => TestData.CreateSaleService(_database.CreateContext());

    private static CreateSaleDto NewSale(params CreateSaleItemDto[] items) =>
        new(null, PaymentMethod.PIX, 0, items);

    [Fact]
    public async Task Create_WithEnoughStock_SavesSaleAndReducesStock()
    {
        var product = await TestData.AddProductAsync(_database, salePrice: 6.50m);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(30));

        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 3)));

        Assert.Equal(SaleStatus.COMPLETED, sale.Status);
        Assert.Equal(19.50m, sale.Subtotal);
        Assert.Equal(19.50m, sale.Total);
        var item = Assert.Single(sale.Items);
        Assert.Equal(6.50m, item.UnitPrice);
        Assert.Equal(7, await TestData.GetStockAsync(_database, product.Id));
        Assert.Equal(7, await TestData.GetBatchQuantityAsync(_database, batch.Id));
        await TestData.AssertStockMatchesBatchesAsync(_database, product.Id);
    }

    [Fact]
    public async Task Create_RegistersSaleMovementForTheBatch()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(30));

        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 4)));

        await using var context = _database.CreateContext();
        var movement = await context.StockMovements.SingleAsync(m => m.MovementType == MovementType.SALE, TestContext.Current.CancellationToken);
        Assert.Equal(batch.Id, movement.BatchId);
        Assert.Equal(-4, movement.Quantity);
        Assert.Equal(10, movement.PreviousStock);
        Assert.Equal(6, movement.NewStock);
        Assert.Equal($"Venda #{sale.Id}", movement.Reason);
        Assert.Equal(("Sale", sale.Id), (movement.ReferenceType, movement.ReferenceId));
    }

    [Fact]
    public async Task Create_UsesTheBatchThatExpiresFirst()
    {
        var product = await TestData.AddProductAsync(_database);
        // Ordem de entrada diferente da ordem de validade: o FEFO olha a validade, não a chegada.
        var later = await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(90), "VENCE-DEPOIS");
        var noExpiry = await TestData.ReceiveBatchAsync(_database, product.Id, 10, null, "SEM-VALIDADE");
        var first = await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(10), "VENCE-ANTES");

        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 4)));

        var used = Assert.Single(Assert.Single(sale.Items).Batches);
        Assert.Equal("VENCE-ANTES", used.BatchNumber);
        Assert.Equal(6, await TestData.GetBatchQuantityAsync(_database, first.Id));
        Assert.Equal(10, await TestData.GetBatchQuantityAsync(_database, later.Id));
        Assert.Equal(10, await TestData.GetBatchQuantityAsync(_database, noExpiry.Id));
    }

    [Fact]
    public async Task Create_UsesBatchesWithoutExpiryLast()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 2, null, "SEM-VALIDADE");
        await TestData.ReceiveBatchAsync(_database, product.Id, 2, Today.AddDays(200), "COM-VALIDADE");

        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 3)));

        Assert.Equal(
            [("COM-VALIDADE", 2), ("SEM-VALIDADE", 1)],
            Assert.Single(sale.Items).Batches.Select(b => (b.BatchNumber, b.Quantity)));
    }

    [Fact]
    public async Task Create_SpansSeveralBatchesWhenOneIsNotEnough()
    {
        var product = await TestData.AddProductAsync(_database);
        var a = await TestData.ReceiveBatchAsync(_database, product.Id, 3, Today.AddDays(10), "A");
        var b = await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(30), "B");

        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 5)));

        Assert.Equal([("A", 3), ("B", 2)], Assert.Single(sale.Items).Batches.Select(x => (x.BatchNumber, x.Quantity)));
        Assert.Equal(0, await TestData.GetBatchQuantityAsync(_database, a.Id));
        Assert.Equal(8, await TestData.GetBatchQuantityAsync(_database, b.Id));
        Assert.Equal(8, await TestData.GetStockAsync(_database, product.Id));

        // Uma movimentação por lote, para o extrato mostrar exatamente de onde saiu cada unidade.
        await using var context = _database.CreateContext();
        var movements = await context.StockMovements
            .Where(m => m.MovementType == MovementType.SALE)
            .OrderBy(m => m.Id)
            .Select(m => new { m.BatchId, m.Quantity, m.PreviousStock, m.NewStock })
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, movements.Count);
        Assert.Equal((a.Id, -3, 13, 10), (movements[0].BatchId!.Value, movements[0].Quantity, movements[0].PreviousStock, movements[0].NewStock));
        Assert.Equal((b.Id, -2, 10, 8), (movements[1].BatchId!.Value, movements[1].Quantity, movements[1].PreviousStock, movements[1].NewStock));
    }

    [Fact]
    public async Task Create_NeverSellsFromExpiredBatches()
    {
        var product = await TestData.AddProductAsync(_database, "Vinho Tinto");
        var expired = await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(-1), "VENCIDO");
        await TestData.ReceiveBatchAsync(_database, product.Id, 2, Today.AddDays(30), "BOM");

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 3))));

        Assert.Contains("Vinho Tinto", exception.Message);
        Assert.Contains("Disponível para venda: 2", exception.Message);
        Assert.Contains("Há 10 unidades vencidas", exception.Message);
        Assert.Equal(12, await TestData.GetStockAsync(_database, product.Id));
        Assert.Equal(10, await TestData.GetBatchQuantityAsync(_database, expired.Id));
    }

    [Fact]
    public async Task Create_BatchExpiringToday_CanStillBeSold()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 5, Today, "VENCE-HOJE");

        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 5)));

        Assert.Equal("VENCE-HOJE", Assert.Single(Assert.Single(sale.Items).Batches).BatchNumber);
    }

    [Fact]
    public async Task Create_WithoutEnoughStock_ThrowsAndChangesNothing()
    {
        var beer = await TestData.AddProductAsync(_database, "Heineken 350ml");
        var gin = await TestData.AddProductAsync(_database, "Gin Tanqueray");
        await TestData.ReceiveBatchAsync(_database, beer.Id, 10, Today.AddDays(30));
        await TestData.ReceiveBatchAsync(_database, gin.Id, 1, Today.AddDays(300));

        // O primeiro item tem estoque, o segundo não: a venda inteira é recusada.
        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService().CreateAsync(NewSale(
            new CreateSaleItemDto(beer.Id, 2),
            new CreateSaleItemDto(gin.Id, 2))));

        Assert.Contains("Estoque insuficiente para \"Gin Tanqueray\"", exception.Message);
        Assert.Equal(10, await TestData.GetStockAsync(_database, beer.Id));
        Assert.Equal(1, await TestData.GetStockAsync(_database, gin.Id));

        await using var context = _database.CreateContext();
        Assert.Empty(context.Sales);
        Assert.Empty(context.StockMovements.Where(m => m.MovementType == MovementType.SALE));
    }

    [Fact]
    public async Task Create_CalculatesTotalsAndProfitFromTheBatchCosts()
    {
        var product = await TestData.AddProductAsync(_database, salePrice: 10.00m);
        await TestData.ReceiveBatchAsync(_database, product.Id, 2, Today.AddDays(10), "A", unitCost: 4.00m);
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(30), "B", unitCost: 5.00m);

        // 3 x R$ 10,00 = 30,00; desconto de R$ 1,00 na linha e de R$ 2,00 na venda.
        var sale = await CreateService().CreateAsync(new CreateSaleDto(
            null, PaymentMethod.CASH, 2.00m, [new CreateSaleItemDto(product.Id, 3, 1.00m)]));

        var item = Assert.Single(sale.Items);
        Assert.Equal(29.00m, item.Total);
        Assert.Equal(4.3333m, item.CostPrice); // (2 x 4,00 + 1 x 5,00) / 3
        Assert.Equal(29.00m, sale.Subtotal);
        Assert.Equal(2.00m, sale.Discount);
        Assert.Equal(27.00m, sale.Total);
        Assert.Equal(13.00m, sale.TotalCost);
        Assert.Equal(14.00m, sale.Profit);
        Assert.Equal(PaymentMethod.CASH, sale.PaymentMethod);
    }

    [Fact]
    public async Task Create_UsesThePriceFromTheProductRegistration()
    {
        var product = await TestData.AddProductAsync(_database, salePrice: 12.90m);
        await TestData.ReceiveBatchAsync(_database, product.Id, 5, Today.AddDays(30));

        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 2)));

        Assert.Equal(25.80m, sale.Total);
    }

    [Fact]
    public async Task Create_SaleDiscountGreaterThanSubtotal_Throws()
    {
        var product = await TestData.AddProductAsync(_database, salePrice: 6.50m);
        await TestData.ReceiveBatchAsync(_database, product.Id, 5, Today.AddDays(30));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService().CreateAsync(
            new CreateSaleDto(null, PaymentMethod.PIX, 13.01m, [new CreateSaleItemDto(product.Id, 2)])));

        Assert.Contains("desconto", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5, await TestData.GetStockAsync(_database, product.Id));
    }

    [Fact]
    public async Task Create_ItemDiscountGreaterThanTheLine_Throws()
    {
        var product = await TestData.AddProductAsync(_database, "Gelo 5kg", salePrice: 10.00m);
        await TestData.ReceiveBatchAsync(_database, product.Id, 5, null);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService().CreateAsync(
            NewSale(new CreateSaleItemDto(product.Id, 1, 10.50m))));

        Assert.Contains("Gelo 5kg", exception.Message);
    }

    [Fact]
    public async Task Create_InactiveProduct_Throws()
    {
        var product = await TestData.AddProductAsync(_database, "Produto Antigo", active: false);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 1))));

        Assert.Contains("inativo", exception.Message);
    }

    [Fact]
    public async Task Create_WithCustomer_LinksTheSale()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 5, Today.AddDays(30));
        var customer = await TestData.AddCustomerAsync(_database, "João Lima");

        var sale = await CreateService().CreateAsync(
            new CreateSaleDto(customer.Id, PaymentMethod.DEBIT_CARD, 0, [new CreateSaleItemDto(product.Id, 1)]));

        Assert.Equal(customer.Id, sale.CustomerId);
        Assert.Equal("João Lima", sale.CustomerName);
    }

    [Fact]
    public async Task Create_InactiveCustomer_Throws()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 5, Today.AddDays(30));
        var customer = await TestData.AddCustomerAsync(_database, active: false);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService().CreateAsync(
            new CreateSaleDto(customer.Id, PaymentMethod.PIX, 0, [new CreateSaleItemDto(product.Id, 1)])));

        Assert.Contains("inativo", exception.Message);
    }

    [Fact]
    public async Task Create_SameProductTwice_ThrowsValidationException()
    {
        var product = await TestData.AddProductAsync(_database);

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().CreateAsync(NewSale(
            new CreateSaleItemDto(product.Id, 1),
            new CreateSaleItemDto(product.Id, 2))));
    }

    [Fact]
    public async Task Cancel_ReturnsEachUnitToTheBatchItCameFrom()
    {
        var product = await TestData.AddProductAsync(_database);
        var a = await TestData.ReceiveBatchAsync(_database, product.Id, 3, Today.AddDays(10), "A");
        var b = await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(30), "B");
        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 5)));

        var canceled = await CreateService().CancelAsync(sale.Id, new CancelSaleDto("  Cliente desistiu  "));

        Assert.Equal(SaleStatus.CANCELED, canceled.Status);
        Assert.Equal("Cliente desistiu", canceled.CancelReason);
        Assert.Equal("Administrador", canceled.CanceledByUserName);
        Assert.NotNull(canceled.CanceledAt);
        Assert.Equal(3, await TestData.GetBatchQuantityAsync(_database, a.Id));
        Assert.Equal(10, await TestData.GetBatchQuantityAsync(_database, b.Id));
        Assert.Equal(13, await TestData.GetStockAsync(_database, product.Id));
        await TestData.AssertStockMatchesBatchesAsync(_database, product.Id);

        await using var context = _database.CreateContext();
        var returns = await context.StockMovements
            .Where(m => m.MovementType == MovementType.RETURN)
            .OrderBy(m => m.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([(a.Id, 3), (b.Id, 2)], returns.Select(m => (m.BatchId!.Value, m.Quantity)));
        Assert.All(returns, m => Assert.Equal($"Cancelamento da venda #{sale.Id}", m.Reason));
        Assert.All(returns, m => Assert.Equal(("Sale", sale.Id), (m.ReferenceType, m.ReferenceId)));
    }

    [Fact]
    public async Task Cancel_TwiceThrows()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 5, Today.AddDays(30));
        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 2)));
        await CreateService().CancelAsync(sale.Id, new CancelSaleDto("Erro no caixa"));

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CancelAsync(sale.Id, new CancelSaleDto("De novo")));

        Assert.Contains("já foi cancelada", exception.Message);
        Assert.Equal(5, await TestData.GetStockAsync(_database, product.Id));
    }

    [Fact]
    public async Task Cancel_WithoutReason_ThrowsValidationException()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 5, Today.AddDays(30));
        var sale = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 1)));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().CancelAsync(sale.Id, new CancelSaleDto(" ")));

        Assert.Contains(exception.Errors, e => e.PropertyName == "Reason");
    }

    [Fact]
    public async Task Cancel_UnknownSale_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().CancelAsync(999, new CancelSaleDto("Teste")));
    }

    [Fact]
    public async Task GetAll_FiltersByStatusAndCustomer()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(30));
        var customer = await TestData.AddCustomerAsync(_database);
        var withCustomer = await CreateService().CreateAsync(
            new CreateSaleDto(customer.Id, PaymentMethod.PIX, 0, [new CreateSaleItemDto(product.Id, 1)]));
        var canceled = await CreateService().CreateAsync(NewSale(new CreateSaleItemDto(product.Id, 1)));
        await CreateService().CancelAsync(canceled.Id, new CancelSaleDto("Teste"));

        var byCustomer = await CreateService().GetAllAsync(new SaleQuery { CustomerId = customer.Id });
        var onlyCanceled = await CreateService().GetAllAsync(new SaleQuery { Status = SaleStatus.CANCELED });
        var today = await CreateService().GetAllAsync(new SaleQuery { From = Today, To = Today });

        Assert.Equal(withCustomer.Id, Assert.Single(byCustomer.Items).Id);
        Assert.Equal(canceled.Id, Assert.Single(onlyCanceled.Items).Id);
        Assert.Equal(2, today.TotalItems);
    }

    public void Dispose() => _database.Dispose();
}
