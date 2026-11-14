using AdegaSystem.Api.Common;

namespace AdegaSystem.Api.DTOs.Customers;

public record CustomerDto(
    int Id,
    string Name,
    string? Cpf,
    string? Phone,
    string? Email,
    DateOnly? BirthDate,
    bool Active,
    DateTime CreatedAt);

/// <summary>Campos comuns ao cadastro e à edição.</summary>
public interface ICustomerInput
{
    string Name { get; }
    string? Cpf { get; }
    string? Phone { get; }
    string? Email { get; }
    DateOnly? BirthDate { get; }
}

/// <summary>Corpo do POST /api/customers. Só o nome é obrigatório (cadastro rápido no PDV). O CPF pode vir com ou sem máscara.</summary>
public record CreateCustomerDto(
    string Name,
    string? Cpf = null,
    string? Phone = null,
    string? Email = null,
    DateOnly? BirthDate = null) : ICustomerInput;

/// <summary>Corpo do PUT /api/customers/{id}.</summary>
public record UpdateCustomerDto(
    string Name,
    string? Cpf,
    string? Phone,
    string? Email,
    DateOnly? BirthDate,
    bool Active) : ICustomerInput;

/// <summary>Filtros do GET /api/customers.</summary>
public class CustomerQuery : PagedQuery
{
    /// <summary>Busca por nome, CPF ou telefone.</summary>
    public string? Search { get; set; }

    public bool? Active { get; set; }
}

/// <summary>
/// Resposta do GET /api/customers/{id}/summary. Conta só as vendas concluídas:
/// uma venda cancelada não é uma compra do cliente.
/// </summary>
public record CustomerSummaryDto(
    int CustomerId,
    string CustomerName,
    int PurchaseCount,
    decimal TotalSpent,
    DateTime? LastPurchaseDate,
    decimal AverageTicket);
