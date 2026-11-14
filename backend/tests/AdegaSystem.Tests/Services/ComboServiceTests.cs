using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Combos;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Services;

public class ComboServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private ComboService CreateService() => TestData.CreateComboService(_database.CreateContext());

    /// <summary>O exemplo do enunciado: 1 whisky + 4 energéticos + 1 gelo = R$ 220,00 separados.</summary>
    private async Task<(Product Whisky, Product Energy, Product Ice)> AddWhiskyComboProductsAsync()
    {
        var whisky = await TestData.AddProductAsync(_database, "Jack Daniel's 1L", costPrice: 110m, salePrice: 150m);
        var energy = await TestData.AddProductAsync(_database, "Red Bull 250ml", costPrice: 8m, salePrice: 15m);
        var ice = await TestData.AddProductAsync(_database, "Gelo 2kg", costPrice: 4m, salePrice: 10m);
        return (whisky, energy, ice);
    }

    [Fact]
    public async Task Create_CalculatesNormalPriceSavingsAndAvailableQuantity()
    {
        var (whisky, energy, ice) = await AddWhiskyComboProductsAsync();
        await TestData.ReceiveBatchAsync(_database, whisky.Id, 3);
        await TestData.ReceiveBatchAsync(_database, energy.Id, 10); // 10 / 4 = 2 combos: o energético limita
        await TestData.ReceiveBatchAsync(_database, ice.Id, 20);

        var combo = await CreateService().CreateAsync(new CreateComboDto("  Combo Whisky  ", null, 199m,
            [new(whisky.Id, 1), new(energy.Id, 4), new(ice.Id, 1)]));

        Assert.Equal("Combo Whisky", combo.Name);
        Assert.Equal(220m, combo.NormalPrice);
        Assert.Equal(21m, combo.Savings);
        Assert.Equal(9.55m, combo.SavingsPercent);
        Assert.Equal(2, combo.AvailableQuantity);
        Assert.True(combo.Active);
        Assert.Equal(3, combo.Items.Count);
    }

    [Fact]
    public async Task Create_PriceAboveTheProductsSeparately_Throws()
    {
        var (whisky, energy, _) = await AddWhiskyComboProductsAsync();

        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService().CreateAsync(
            new CreateComboDto("Caro demais", null, 200m, [new(whisky.Id, 1), new(energy.Id, 2)])));

        Assert.Equal("O preço do combo (R$ 200,00) não pode ser maior que a soma dos produtos separados (R$ 180,00).", exception.Message);
    }

    [Fact]
    public async Task Create_WithInactiveProduct_Throws()
    {
        var inactive = await TestData.AddProductAsync(_database, "Fora de linha", active: false);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService().CreateAsync(
            new CreateComboDto("Combo", null, 5m, [new(inactive.Id, 1)])));

        Assert.Contains("está inativo", exception.Message);
    }

    [Fact]
    public async Task Create_SameProductTwice_ThrowsValidationException()
    {
        var (whisky, _, _) = await AddWhiskyComboProductsAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateService().CreateAsync(
            new CreateComboDto("Combo", null, 100m, [new(whisky.Id, 1), new(whisky.Id, 2)])));

        Assert.Contains(exception.Errors, e => e.ErrorMessage == "O mesmo produto aparece mais de uma vez. Junte as quantidades numa linha só.");
    }

    [Fact]
    public async Task Update_ReplacesTheItems_AndIsAudited()
    {
        var (whisky, energy, ice) = await AddWhiskyComboProductsAsync();
        var combo = await CreateService().CreateAsync(new CreateComboDto("Combo Whisky", null, 199m,
            [new(whisky.Id, 1), new(energy.Id, 4), new(ice.Id, 1)]));

        var updated = await CreateService().UpdateAsync(combo.Id, new UpdateComboDto("Combo Whisky", "Sem gelo", 185m, true,
            [new(whisky.Id, 1), new(energy.Id, 3)]));

        Assert.Equal(185m, updated.SalePrice);
        Assert.Equal(195m, updated.NormalPrice);
        Assert.Equal([whisky.Id, energy.Id], updated.Items.Select(i => i.ProductId).Order());

        await using var context = _database.CreateContext();
        var log = await context.AuditLogs.SingleAsync(a => a.Action == AuditAction.COMBO_UPDATED, TestContext.Current.CancellationToken);
        Assert.Equal(combo.Id, log.EntityId);
        Assert.Contains("\"salePrice\":199", log.OldValue);
        Assert.Contains("1x Gelo 2kg", log.OldValue);
        Assert.Contains("\"salePrice\":185", log.NewValue);
        Assert.DoesNotContain("Gelo", log.NewValue);
    }

    [Fact]
    public async Task Deactivate_KeepsTheComboAndRegistersAudit()
    {
        var (whisky, _, _) = await AddWhiskyComboProductsAsync();
        var combo = await CreateService().CreateAsync(new CreateComboDto("Combo", null, 140m, [new(whisky.Id, 1)]));

        await CreateService().DeactivateAsync(combo.Id);

        var active = await CreateService().GetAllAsync(new ComboQuery { Active = true });
        Assert.Empty(active.Items);
        Assert.False((await CreateService().GetByIdAsync(combo.Id)).Active);

        await using var context = _database.CreateContext();
        Assert.True(await context.AuditLogs.AnyAsync(a => a.Action == AuditAction.COMBO_DEACTIVATED && a.EntityId == combo.Id,
            TestContext.Current.CancellationToken));
    }

    public void Dispose() => _database.Dispose();
}
