using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Suppliers;

namespace AdegaSystem.Api.Services;

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetAllAsync(SupplierQuery query);
    Task<SupplierDto> GetByIdAsync(int id);
    Task<SupplierSummaryDto> GetSummaryAsync(int id);
    Task<SupplierDto> CreateAsync(CreateSupplierDto dto);
    Task<SupplierDto> UpdateAsync(int id, UpdateSupplierDto dto);
    Task DeactivateAsync(int id);
}
