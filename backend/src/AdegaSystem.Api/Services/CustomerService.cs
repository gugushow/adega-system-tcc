using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Customers;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Mappings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

public class CustomerService(
    AdegaDbContext context,
    IValidator<CreateCustomerDto> createValidator,
    IValidator<UpdateCustomerDto> updateValidator) : ICustomerService
{
    public async Task<PagedResult<CustomerDto>> GetAllAsync(CustomerQuery query)
    {
        var customers = context.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            // "529.982" e "529982" encontram o mesmo CPF; telefone é procurado como foi digitado.
            var digits = Cpf.Normalize(query.Search);
            customers = customers.Where(c =>
                c.Name.ToLower().Contains(search) ||
                (digits != "" && c.Cpf != null && c.Cpf.Contains(digits)) ||
                (c.Phone != null && c.Phone.Contains(search)));
        }

        if (query.Active.HasValue)
        {
            customers = customers.Where(c => c.Active == query.Active.Value);
        }

        var totalItems = await customers.CountAsync();
        var page = await customers
            .OrderBy(c => c.Name)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<CustomerDto>(page.Select(c => c.ToDto()).ToList(), query.Page, query.PageSize, totalItems);
    }

    public async Task<CustomerDto> GetByIdAsync(int id)
    {
        var customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Cliente não encontrado.");

        return customer.ToDto();
    }

    public async Task<CustomerSummaryDto> GetSummaryAsync(int id)
    {
        var customer = await GetByIdAsync(id);

        // Só vendas concluídas: uma venda cancelada não é compra do cliente.
        var sales = await context.Sales.AsNoTracking()
            .Where(s => s.CustomerId == id && s.Status == SaleStatus.COMPLETED)
            .Select(s => new { s.Total, s.SaleDate })
            .ToListAsync();

        // Somas em memória, como no resumo do fornecedor (o SQLite dos testes não soma decimal).
        var totalSpent = sales.Sum(s => s.Total);
        var averageTicket = sales.Count == 0 ? 0 : Math.Round(totalSpent / sales.Count, 2, MidpointRounding.AwayFromZero);

        return new CustomerSummaryDto(
            customer.Id,
            customer.Name,
            sales.Count,
            totalSpent,
            sales.Count == 0 ? null : sales.Max(s => s.SaleDate),
            averageTicket);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        await createValidator.ValidateAndThrowAsync(dto);

        var cpf = NormalizeCpf(dto.Cpf);
        await EnsureCpfIsAvailableAsync(cpf, ignoreId: null);

        var customer = new Customer();
        ApplyInput(customer, dto, cpf);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        return customer.ToDto();
    }

    public async Task<CustomerDto> UpdateAsync(int id, UpdateCustomerDto dto, bool canChangeStatus)
    {
        await updateValidator.ValidateAndThrowAsync(dto);

        var customer = await FindAsync(id);
        var cpf = NormalizeCpf(dto.Cpf);
        await EnsureCpfIsAvailableAsync(cpf, ignoreId: id);

        ApplyInput(customer, dto, cpf);
        // Desativar cliente é só do ADMIN (igual à rota DELETE). O vendedor edita os dados,
        // mas a situação fica como estava, mesmo que a requisição mande outro valor.
        if (canChangeStatus)
        {
            customer.Active = dto.Active;
        }
        await context.SaveChangesAsync();

        return customer.ToDto();
    }

    public async Task DeactivateAsync(int id)
    {
        var customer = await FindAsync(id);

        // Soft delete: as vendas antigas continuam apontando para ele.
        customer.Active = false;
        await context.SaveChangesAsync();
    }

    private static void ApplyInput(Customer customer, ICustomerInput input, string? cpf)
    {
        customer.Name = input.Name.Trim();
        customer.Cpf = cpf;
        customer.Phone = Text.EmptyToNull(input.Phone);
        customer.Email = Text.EmptyToNull(input.Email);
        customer.BirthDate = input.BirthDate;
    }

    /// <summary>Grava sempre só os 11 dígitos, para o índice único funcionar com ou sem máscara.</summary>
    private static string? NormalizeCpf(string? cpf) => string.IsNullOrWhiteSpace(cpf) ? null : Cpf.Normalize(cpf);

    private async Task<Customer> FindAsync(int id) =>
        await context.Customers.FindAsync(id)
        ?? throw new NotFoundException("Cliente não encontrado.");

    private async Task EnsureCpfIsAvailableAsync(string? cpf, int? ignoreId)
    {
        if (cpf is null)
        {
            return;
        }

        var exists = await context.Customers.AnyAsync(c => c.Cpf == cpf && (ignoreId == null || c.Id != ignoreId));
        if (exists)
        {
            throw new BusinessException("Já existe um cliente com esse CPF.");
        }
    }
}
