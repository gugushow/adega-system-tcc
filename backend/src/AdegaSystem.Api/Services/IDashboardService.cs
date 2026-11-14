using AdegaSystem.Api.DTOs.Dashboard;

namespace AdegaSystem.Api.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync();
}
