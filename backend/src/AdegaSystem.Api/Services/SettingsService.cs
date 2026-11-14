using System.Globalization;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.Data.Seed;
using AdegaSystem.Api.DTOs.Settings;
using AdegaSystem.Api.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Lê e grava as configurações da tabela SystemSettings. Os números são gravados com
/// CultureInfo.InvariantCulture ("15.5"), para não depender do idioma do servidor ("15,5").
/// Toda alteração entra na auditoria.
/// </summary>
public class SettingsService(
    AdegaDbContext context,
    IAuditService auditService,
    IValidator<MarginSettingsDto> marginValidator,
    IValidator<LoyaltySettingsDto> loyaltyValidator) : ISettingsService
{
    // Usados se a linha não existir no banco (a migration sempre cria).
    public const decimal DefaultMinimumMarginPercent = 15m;
    public const bool DefaultLoyaltyEnabled = true;
    public const decimal DefaultLoyaltyAmountPerPoint = 10m;

    public async Task<MarginSettingsDto> GetMarginAsync() =>
        new(await GetDecimalAsync(SystemSettingSeed.MinimumMarginPercentKey, DefaultMinimumMarginPercent));

    public async Task<MarginSettingsDto> UpdateMarginAsync(MarginSettingsDto dto)
    {
        await marginValidator.ValidateAndThrowAsync(dto);

        var before = await GetMarginAsync();
        var value = Math.Round(dto.MinimumMarginPercent, 2);
        await SetAsync(SystemSettingSeed.MinimumMarginPercentKey, value.ToString(CultureInfo.InvariantCulture));

        var after = new MarginSettingsDto(value);
        // Records comparam campo a campo: salvar sem mudar nada não gera registro de auditoria.
        if (after != before)
        {
            auditService.Add(AuditAction.SETTINGS_UPDATED, nameof(SystemSetting), 0, "Margem mínima", before, after);
        }
        await context.SaveChangesAsync();
        return after;
    }

    public async Task<LoyaltySettingsDto> GetLoyaltyAsync()
    {
        var enabled = await GetValueAsync(SystemSettingSeed.LoyaltyEnabledKey);
        return new LoyaltySettingsDto(
            bool.TryParse(enabled, out var parsed) ? parsed : DefaultLoyaltyEnabled,
            await GetDecimalAsync(SystemSettingSeed.LoyaltyAmountPerPointKey, DefaultLoyaltyAmountPerPoint));
    }

    public async Task<LoyaltySettingsDto> UpdateLoyaltyAsync(LoyaltySettingsDto dto)
    {
        await loyaltyValidator.ValidateAndThrowAsync(dto);

        var before = await GetLoyaltyAsync();
        var after = dto with { AmountPerPoint = Math.Round(dto.AmountPerPoint, 2) };
        // "true"/"false" em minúsculas, como no seed.
        await SetAsync(SystemSettingSeed.LoyaltyEnabledKey, after.Enabled ? "true" : "false");
        await SetAsync(SystemSettingSeed.LoyaltyAmountPerPointKey, after.AmountPerPoint.ToString(CultureInfo.InvariantCulture));

        if (after != before)
        {
            auditService.Add(AuditAction.SETTINGS_UPDATED, nameof(SystemSetting), 0, "Programa de fidelidade", before, after);
        }
        await context.SaveChangesAsync();
        return after;
    }

    private async Task<string?> GetValueAsync(string key) =>
        await context.SystemSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

    private async Task<decimal> GetDecimalAsync(string key, decimal defaultValue) =>
        decimal.TryParse(await GetValueAsync(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;

    /// <summary>Altera o valor no contexto (cria a linha se faltar). Quem chama faz o SaveChanges.</summary>
    private async Task SetAsync(string key, string value)
    {
        var setting = await context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
        {
            setting = new SystemSetting { Key = key };
            context.SystemSettings.Add(setting);
        }

        setting.Value = value;
    }
}
