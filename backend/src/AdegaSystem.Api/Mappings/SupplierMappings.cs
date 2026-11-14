using AdegaSystem.Api.DTOs.Suppliers;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Mappings;

public static class SupplierMappings
{
    public static SupplierDto ToDto(this Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.CorporateName,
        supplier.Cnpj,
        supplier.Phone,
        supplier.Email,
        supplier.Address,
        supplier.Active,
        supplier.CreatedAt);
}
