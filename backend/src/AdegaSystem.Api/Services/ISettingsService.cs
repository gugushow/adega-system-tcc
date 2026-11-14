using AdegaSystem.Api.DTOs.Settings;

namespace AdegaSystem.Api.Services;

public interface ISettingsService
{
    Task<MarginSettingsDto> GetMarginAsync();
    Task<MarginSettingsDto> UpdateMarginAsync(MarginSettingsDto dto);

    Task<LoyaltySettingsDto> GetLoyaltyAsync();
    Task<LoyaltySettingsDto> UpdateLoyaltyAsync(LoyaltySettingsDto dto);
}
