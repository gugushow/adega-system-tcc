using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Settings;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>
/// Configurações do sistema. Consultar é para todos (as telas mostram o alerta de margem e os pontos do cliente);
/// alterar, só ADMIN.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController(ISettingsService settingsService) : ControllerBase
{
    [HttpGet("margin")]
    public async Task<ActionResult<ApiResponse<MarginSettingsDto>>> GetMargin() =>
        Ok(ApiResponse<MarginSettingsDto>.Ok(await settingsService.GetMarginAsync()));

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("margin")]
    public async Task<ActionResult<ApiResponse<MarginSettingsDto>>> UpdateMargin(MarginSettingsDto dto) =>
        Ok(ApiResponse<MarginSettingsDto>.Ok(await settingsService.UpdateMarginAsync(dto), "Margem mínima atualizada."));

    [HttpGet("loyalty")]
    public async Task<ActionResult<ApiResponse<LoyaltySettingsDto>>> GetLoyalty() =>
        Ok(ApiResponse<LoyaltySettingsDto>.Ok(await settingsService.GetLoyaltyAsync()));

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("loyalty")]
    public async Task<ActionResult<ApiResponse<LoyaltySettingsDto>>> UpdateLoyalty(LoyaltySettingsDto dto) =>
        Ok(ApiResponse<LoyaltySettingsDto>.Ok(await settingsService.UpdateLoyaltyAsync(dto), "Programa de fidelidade atualizado."));
}
