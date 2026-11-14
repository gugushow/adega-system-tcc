using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Combos;

namespace AdegaSystem.Api.Services;

public interface IComboService
{
    Task<PagedResult<ComboDto>> GetAllAsync(ComboQuery query);
    Task<ComboDto> GetByIdAsync(int id);
    Task<ComboDto> CreateAsync(CreateComboDto dto);
    Task<ComboDto> UpdateAsync(int id, UpdateComboDto dto);
    Task DeactivateAsync(int id);
}
