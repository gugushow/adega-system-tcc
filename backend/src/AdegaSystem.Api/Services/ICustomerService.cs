using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Customers;

namespace AdegaSystem.Api.Services;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetAllAsync(CustomerQuery query);
    Task<CustomerDto> GetByIdAsync(int id);
    Task<CustomerSummaryDto> GetSummaryAsync(int id);
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto);
    /// <param name="canChangeStatus">Só o ADMIN ativa/desativa clientes; para os outros perfis o campo Active é ignorado.</param>
    Task<CustomerDto> UpdateAsync(int id, UpdateCustomerDto dto, bool canChangeStatus);
    Task DeactivateAsync(int id);
}
