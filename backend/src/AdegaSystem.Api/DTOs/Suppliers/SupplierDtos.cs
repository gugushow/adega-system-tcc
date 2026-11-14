using AdegaSystem.Api.Common;

namespace AdegaSystem.Api.DTOs.Suppliers;

public record SupplierDto(
    int Id,
    string Name,
    string? CorporateName,
    string? Cnpj,
    string? Phone,
    string? Email,
    string? Address,
    bool Active,
    DateTime CreatedAt);

/// <summary>Campos comuns ao cadastro e à edição (mesma ideia do IProductInput).</summary>
public interface ISupplierInput
{
    string Name { get; }
    string? CorporateName { get; }
    string? Cnpj { get; }
    string? Phone { get; }
    string? Email { get; }
    string? Address { get; }
}

/// <summary>Corpo do POST /api/suppliers. O CNPJ pode vir com ou sem máscara.</summary>
public record CreateSupplierDto(
    string Name,
    string? CorporateName,
    string? Cnpj,
    string? Phone,
    string? Email,
    string? Address) : ISupplierInput;

/// <summary>Corpo do PUT /api/suppliers/{id}.</summary>
public record UpdateSupplierDto(
    string Name,
    string? CorporateName,
    string? Cnpj,
    string? Phone,
    string? Email,
    string? Address,
    bool Active) : ISupplierInput;

/// <summary>Filtros do GET /api/suppliers.</summary>
public class SupplierQuery : PagedQuery
{
    /// <summary>Busca por nome, razão social ou CNPJ.</summary>
    public string? Search { get; set; }

    public bool? Active { get; set; }
}

/// <summary>Resposta do GET /api/suppliers/{id}/summary.</summary>
public record SupplierSummaryDto(
    int SupplierId,
    string SupplierName,
    int PurchaseCount,
    decimal TotalPurchased,
    DateOnly? LastPurchaseDate,
    IReadOnlyList<SupplierTopProductDto> TopProducts);

public record SupplierTopProductDto(int ProductId, string ProductName, int TotalQuantity, decimal TotalAmount);
