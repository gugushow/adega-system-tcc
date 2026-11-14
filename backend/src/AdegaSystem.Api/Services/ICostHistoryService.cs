using AdegaSystem.Api.DTOs.CostHistory;

namespace AdegaSystem.Api.Services;

public interface ICostHistoryService
{
    Task<CostHistoryDto> GetByProductAsync(int productId);
}
