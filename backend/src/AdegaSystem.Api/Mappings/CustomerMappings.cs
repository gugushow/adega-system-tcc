using AdegaSystem.Api.DTOs.Customers;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Mappings;

public static class CustomerMappings
{
    public static CustomerDto ToDto(this Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Cpf,
        customer.Phone,
        customer.Email,
        customer.BirthDate,
        customer.Active,
        customer.CreatedAt);
}
