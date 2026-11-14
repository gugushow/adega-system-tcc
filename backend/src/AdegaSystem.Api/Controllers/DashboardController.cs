using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Dashboard;
using AdegaSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdegaSystem.Api.Controllers;

/// <summary>Números e gráficos da tela inicial. Faturamento e lucro são do ADMIN.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.Admin)]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> Get() =>
        Ok(ApiResponse<DashboardDto>.Ok(await dashboardService.GetAsync()));
}
