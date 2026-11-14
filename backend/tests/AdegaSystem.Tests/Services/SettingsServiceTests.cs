using AdegaSystem.Api.Data.Seed;
using AdegaSystem.Api.DTOs.Settings;
using AdegaSystem.Api.Entities;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    [Fact]
    public async Task GetMargin_StartsWithTheSeededFifteenPercent()
    {
        var settings = await TestData.CreateSettingsService(_database.CreateContext()).GetMarginAsync();

        Assert.Equal(15m, settings.MinimumMarginPercent);
    }

    [Fact]
    public async Task UpdateMargin_StoresTheValueWithoutDependingOnCulture()
    {
        await TestData.CreateSettingsService(_database.CreateContext()).UpdateMarginAsync(new MarginSettingsDto(20.5m));

        var settings = await TestData.CreateSettingsService(_database.CreateContext()).GetMarginAsync();
        await using var context = _database.CreateContext();
        var stored = await context.SystemSettings.SingleAsync(s => s.Key == SystemSettingSeed.MinimumMarginPercentKey, TestContext.Current.CancellationToken);

        Assert.Equal(20.5m, settings.MinimumMarginPercent);
        Assert.Equal("20.5", stored.Value); // ponto, nunca "20,5"
    }

    [Theory]
    [InlineData(-1, "A margem mínima não pode ser negativa.")]
    [InlineData(100, "A margem mínima deve ser menor que 100%.")]
    public async Task UpdateMargin_OutOfRange_ThrowsValidationException(decimal value, string message)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            TestData.CreateSettingsService(_database.CreateContext()).UpdateMarginAsync(new MarginSettingsDto(value)));

        Assert.Contains(exception.Errors, e => e.ErrorMessage == message);
    }

    [Fact]
    public async Task GetLoyalty_StartsEnabledWithTenReaisPerPoint()
    {
        var settings = await TestData.CreateSettingsService(_database.CreateContext()).GetLoyaltyAsync();

        Assert.Equal(new LoyaltySettingsDto(true, 10m), settings);
    }

    [Fact]
    public async Task UpdateLoyalty_StoresTheValues_AndIsAudited()
    {
        await TestData.CreateSettingsService(_database.CreateContext()).UpdateLoyaltyAsync(new LoyaltySettingsDto(false, 12.5m));

        var settings = await TestData.CreateSettingsService(_database.CreateContext()).GetLoyaltyAsync();
        Assert.Equal(new LoyaltySettingsDto(false, 12.5m), settings);

        await using var context = _database.CreateContext();
        var stored = await context.SystemSettings.SingleAsync(s => s.Key == SystemSettingSeed.LoyaltyAmountPerPointKey, TestContext.Current.CancellationToken);
        Assert.Equal("12.5", stored.Value);
        var log = await context.AuditLogs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((AuditAction.SETTINGS_UPDATED, "Programa de fidelidade"), (log.Action, log.Description));
        Assert.Equal("{\"enabled\":true,\"amountPerPoint\":10}", log.OldValue);
        Assert.Equal("{\"enabled\":false,\"amountPerPoint\":12.5}", log.NewValue);
    }

    [Theory]
    [InlineData(0, "O valor por ponto deve ser maior que zero.")]
    [InlineData(10.555, "O valor por ponto aceita no máximo 2 casas decimais.")]
    public async Task UpdateLoyalty_InvalidAmount_ThrowsValidationException(decimal value, string message)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            TestData.CreateSettingsService(_database.CreateContext()).UpdateLoyaltyAsync(new LoyaltySettingsDto(true, value)));

        Assert.Contains(exception.Errors, e => e.ErrorMessage == message);
    }

    public void Dispose() => _database.Dispose();
}
