using AdegaSystem.Api.DTOs.Loyalty;
using AdegaSystem.Api.DTOs.Purchases;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Validators;

namespace AdegaSystem.Tests.Validators;

/// <summary>
/// O limite de quantidade (QuantityRules.Max = 100.000) vale para compra, venda, ajuste e pontos.
/// Aqui chamamos os validators direto, sem banco: a regra é só de formato.
/// </summary>
public class QuantityRulesTests
{
    private const int AboveLimit = QuantityRules.Max + 1;
    private const string LimitMessage = "A quantidade deve ser de no máximo 100.000.";

    [Fact]
    public void SaleItem_RejectsQuantityAboveTheLimit()
    {
        var result = new CreateSaleItemDtoValidator().Validate(new CreateSaleItemDto(1, AboveLimit));

        Assert.Contains(result.Errors, e => e.PropertyName == "Quantity" && e.ErrorMessage == LimitMessage);
    }

    [Fact]
    public void SaleItem_AcceptsQuantityAtTheLimit() =>
        Assert.True(new CreateSaleItemDtoValidator().Validate(new CreateSaleItemDto(1, QuantityRules.Max)).IsValid);

    [Fact]
    public void PurchaseItem_RejectsQuantityAboveTheLimit()
    {
        var result = new CreatePurchaseItemDtoValidator().Validate(new CreatePurchaseItemDto(1, AboveLimit, 1m));

        Assert.Contains(result.Errors, e => e.PropertyName == "Quantity" && e.ErrorMessage == LimitMessage);
    }

    [Fact]
    public void PurchaseItem_AcceptsQuantityAtTheLimit() =>
        Assert.True(new CreatePurchaseItemDtoValidator().Validate(new CreatePurchaseItemDto(1, QuantityRules.Max, 1m)).IsValid);

    [Theory]
    [InlineData(MovementType.ADJUSTMENT, AboveLimit)]  // entrada absurda
    [InlineData(MovementType.ADJUSTMENT, -AboveLimit)] // saída absurda
    [InlineData(MovementType.LOSS, -AboveLimit)]
    public void StockAdjustment_RejectsQuantityAboveTheLimit(MovementType type, int quantity)
    {
        var result = new StockAdjustmentDtoValidator().Validate(new StockAdjustmentDto(1, type, quantity, "Contagem"));

        Assert.Contains(result.Errors, e => e.PropertyName == "Quantity" && e.ErrorMessage == LimitMessage);
    }

    [Theory]
    [InlineData(MovementType.ADJUSTMENT, QuantityRules.Max)]
    [InlineData(MovementType.ADJUSTMENT, -QuantityRules.Max)]
    [InlineData(MovementType.LOSS, -QuantityRules.Max)]
    public void StockAdjustment_AcceptsQuantityAtTheLimit(MovementType type, int quantity) =>
        Assert.True(new StockAdjustmentDtoValidator().Validate(new StockAdjustmentDto(1, type, quantity, "Contagem")).IsValid);

    [Fact]
    public void SaleCombo_RejectsQuantityAboveTheLimit()
    {
        var sale = new CreateSaleDto(null, PaymentMethod.PIX, 0, [], [new CreateSaleComboDto(1, AboveLimit)]);

        var result = new CreateSaleDtoValidator().Validate(sale);

        Assert.Contains(result.Errors, e => e.PropertyName == "Combos[0].Quantity" && e.ErrorMessage == LimitMessage);
    }

    [Fact]
    public void SaleCombo_AcceptsQuantityAtTheLimit()
    {
        var sale = new CreateSaleDto(null, PaymentMethod.PIX, 0, [], [new CreateSaleComboDto(1, QuantityRules.Max)]);

        Assert.True(new CreateSaleDtoValidator().Validate(sale).IsValid);
    }

    [Fact]
    public void RedeemPoints_RejectsPointsAboveTheLimit()
    {
        var result = new RedeemPointsDtoValidator().Validate(new RedeemPointsDto(AboveLimit, "Taça de cristal"));

        Assert.Contains(result.Errors, e =>
            e.PropertyName == "Points" && e.ErrorMessage == "O resgate deve ser de no máximo 100.000 pontos.");
    }

    [Fact]
    public void RedeemPoints_AcceptsPointsAtTheLimit() =>
        Assert.True(new RedeemPointsDtoValidator().Validate(new RedeemPointsDto(QuantityRules.Max, "Taça de cristal")).IsValid);

    [Theory]
    [InlineData(AboveLimit)]
    [InlineData(-AboveLimit)]
    public void AdjustPoints_RejectsPointsAboveTheLimit(int points)
    {
        var result = new AdjustPointsDtoValidator().Validate(new AdjustPointsDto(points, "Correção"));

        Assert.Contains(result.Errors, e =>
            e.PropertyName == "Points" && e.ErrorMessage == "O ajuste deve ser de no máximo 100.000 pontos.");
    }

    [Theory]
    [InlineData(QuantityRules.Max)]
    [InlineData(-QuantityRules.Max)]
    public void AdjustPoints_AcceptsPointsAtTheLimit(int points) =>
        Assert.True(new AdjustPointsDtoValidator().Validate(new AdjustPointsDto(points, "Correção")).IsValid);
}
