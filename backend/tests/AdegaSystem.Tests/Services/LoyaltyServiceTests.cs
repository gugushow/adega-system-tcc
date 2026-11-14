using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Loyalty;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.DTOs.Settings;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Services;

public class LoyaltyServiceTests : IDisposable
{
    private static readonly DateOnly Today = BusinessClock.Today;

    private readonly TestDatabase _database = new();

    private LoyaltyService CreateService(int userId = TestData.AdminUserId) =>
        TestData.CreateLoyaltyService(_database.CreateContext(), userId);

    /// <summary>Produto de R$ 59,90 com estoque, e uma venda para o cliente (quantidade escolhida).</summary>
    private async Task<SaleDetailsDto> SellAsync(int? customerId, int quantity = 1, decimal discount = 0)
    {
        var product = await TestData.AddProductAsync(_database, $"Vinho {Guid.NewGuid():N}", costPrice: 30m, salePrice: 59.90m);
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(200), unitCost: 30m);
        return await TestData.CreateSaleService(_database.CreateContext())
            .CreateAsync(new CreateSaleDto(customerId, PaymentMethod.PIX, discount, [new CreateSaleItemDto(product.Id, quantity)]));
    }

    private Task<LoyaltyStatementDto> StatementAsync(int customerId) => CreateService().GetStatementAsync(customerId, new PagedQuery());

    [Fact]
    public async Task Sale_WithCustomer_EarnsOnePointPerTenReais_RoundingDown()
    {
        var customer = await TestData.AddCustomerAsync(_database);

        var sale = await SellAsync(customer.Id, quantity: 2); // R$ 119,80 → 11 pontos

        Assert.Equal(11, sale.LoyaltyPoints);
        var statement = await StatementAsync(customer.Id);
        Assert.Equal(11, statement.Balance);
        var line = Assert.Single(statement.Transactions.Items);
        Assert.Equal((LoyaltyType.EARN, 11, $"Venda #{sale.Id}", sale.Id), (line.Type, line.Points, line.Description, line.SaleId));
    }

    [Fact]
    public async Task Sale_PointsUseTheTotalAfterDiscount()
    {
        var customer = await TestData.AddCustomerAsync(_database);

        var sale = await SellAsync(customer.Id, quantity: 2, discount: 20m); // R$ 99,80 → 9 pontos

        Assert.Equal(9, sale.LoyaltyPoints);
    }

    [Fact]
    public async Task Sale_WithoutCustomer_EarnsNothing()
    {
        var sale = await SellAsync(customerId: null);

        Assert.Equal(0, sale.LoyaltyPoints);
        await using var context = _database.CreateContext();
        Assert.False(await context.LoyaltyTransactions.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sale_WithProgramDisabled_EarnsNothing()
    {
        var customer = await TestData.AddCustomerAsync(_database);
        await TestData.CreateSettingsService(_database.CreateContext()).UpdateLoyaltyAsync(new LoyaltySettingsDto(false, 10m));

        var sale = await SellAsync(customer.Id);

        Assert.Equal(0, sale.LoyaltyPoints);
        Assert.Equal(0, (await StatementAsync(customer.Id)).Balance);
    }

    [Fact]
    public async Task Sale_UsesTheConfiguredAmountPerPoint()
    {
        var customer = await TestData.AddCustomerAsync(_database);
        await TestData.CreateSettingsService(_database.CreateContext()).UpdateLoyaltyAsync(new LoyaltySettingsDto(true, 5m));

        var sale = await SellAsync(customer.Id); // R$ 59,90 / 5 = 11 pontos

        Assert.Equal(11, sale.LoyaltyPoints);
    }

    [Fact]
    public async Task CancelSale_ReversesThePoints_EvenIfTheBalanceGoesNegative()
    {
        var customer = await TestData.AddCustomerAsync(_database);
        var sale = await SellAsync(customer.Id, quantity: 2); // 11 pontos
        await CreateService().RedeemAsync(customer.Id, new RedeemPointsDto(10, "Taça de brinde"));

        await TestData.CreateSaleService(_database.CreateContext()).CancelAsync(sale.Id, new CancelSaleDto("Devolução"));

        var statement = await StatementAsync(customer.Id);
        Assert.Equal(-10, statement.Balance); // 11 − 10 − 11
        var reversal = statement.Transactions.Items[0];
        Assert.Equal((LoyaltyType.ADJUSTMENT, -11, $"Estorno: venda #{sale.Id} cancelada"), (reversal.Type, reversal.Points, reversal.Description));
    }

    [Fact]
    public async Task Redeem_TakesPointsFromTheBalance_AndIsAudited()
    {
        var customer = await TestData.AddCustomerAsync(_database);
        await SellAsync(customer.Id, quantity: 2); // 11 pontos

        var statement = await CreateService().RedeemAsync(customer.Id, new RedeemPointsDto(5, "  Desconto no balcão  "));

        Assert.Equal(6, statement.Balance);
        var line = statement.Transactions.Items[0];
        Assert.Equal((LoyaltyType.REDEEM, -5, "Desconto no balcão"), (line.Type, line.Points, line.Description));

        await using var context = _database.CreateContext();
        var log = await context.AuditLogs.SingleAsync(a => a.Action == AuditAction.LOYALTY_REDEEMED, TestContext.Current.CancellationToken);
        Assert.Equal(("Customer", customer.Id), (log.Entity, log.EntityId));
        Assert.Contains("\"balance\":11", log.OldValue);
        Assert.Contains("\"balance\":6", log.NewValue);
    }

    [Fact]
    public async Task Redeem_MoreThanTheBalance_Throws()
    {
        var customer = await TestData.AddCustomerAsync(_database);
        await SellAsync(customer.Id); // 5 pontos

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateService().RedeemAsync(customer.Id, new RedeemPointsDto(6, "Brinde")));

        Assert.Equal("Saldo insuficiente. O cliente tem 5 ponto(s) e o resgate pede 6.", exception.Message);
    }

    [Fact]
    public async Task Adjust_AddsOrRemovesPoints_ButNeverBelowZero()
    {
        var customer = await TestData.AddCustomerAsync(_database);

        var statement = await CreateService().AdjustAsync(customer.Id, new AdjustPointsDto(20, "Pontos da ficha de papel"));
        Assert.Equal(20, statement.Balance);

        statement = await CreateService().AdjustAsync(customer.Id, new AdjustPointsDto(-5, "Correção"));
        Assert.Equal(15, statement.Balance);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateService().AdjustAsync(customer.Id, new AdjustPointsDto(-16, "Correção")));
        Assert.Equal("O ajuste deixaria o saldo negativo. O cliente tem 15 ponto(s).", exception.Message);
    }

    [Fact]
    public async Task Redeem_InactiveCustomer_Throws()
    {
        var customer = await TestData.AddCustomerAsync(_database, active: false);

        await Assert.ThrowsAsync<BusinessException>(() => CreateService().RedeemAsync(customer.Id, new RedeemPointsDto(1, "Brinde")));
    }

    [Fact]
    public async Task Statement_UnknownCustomer_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => StatementAsync(999));
    }

    public void Dispose() => _database.Dispose();
}
