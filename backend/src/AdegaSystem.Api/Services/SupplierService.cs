using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Suppliers;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Mappings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

public class SupplierService(
    AdegaDbContext context,
    IValidator<CreateSupplierDto> createValidator,
    IValidator<UpdateSupplierDto> updateValidator) : ISupplierService
{
    private const int TopProductsCount = 5;

    public async Task<PagedResult<SupplierDto>> GetAllAsync(SupplierQuery query)
    {
        var suppliers = context.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            var cnpjSearch = Cnpj.Normalize(query.Search);
            suppliers = suppliers.Where(s =>
                s.Name.ToLower().Contains(search) ||
                (s.CorporateName != null && s.CorporateName.ToLower().Contains(search)) ||
                (cnpjSearch != "" && s.Cnpj != null && s.Cnpj.Contains(cnpjSearch)));
        }

        if (query.Active.HasValue)
        {
            suppliers = suppliers.Where(s => s.Active == query.Active.Value);
        }

        var totalItems = await suppliers.CountAsync();
        var page = await suppliers
            .OrderBy(s => s.Name)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<SupplierDto>(page.Select(s => s.ToDto()).ToList(), query.Page, query.PageSize, totalItems);
    }

    public async Task<SupplierDto> GetByIdAsync(int id)
    {
        var supplier = await context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException("Fornecedor não encontrado.");

        return supplier.ToDto();
    }

    public async Task<SupplierSummaryDto> GetSummaryAsync(int id)
    {
        var supplier = await GetByIdAsync(id);

        var purchases = await context.Purchases.AsNoTracking()
            .Where(p => p.SupplierId == id)
            .Select(p => new { p.TotalAmount, p.PurchaseDate })
            .ToListAsync();

        var items = await context.PurchaseItems.AsNoTracking()
            .Where(i => i.Purchase.SupplierId == id)
            .Select(i => new { i.ProductId, ProductName = i.Product.Name, i.Quantity, i.Total })
            .ToListAsync();

        // As somas e o ranking são feitos em memória, depois do ToListAsync. É simples e o volume
        // de compras de um fornecedor é pequeno. (O SQLite usado nos testes nem sabe somar decimal.)
        var topProducts = items
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new SupplierTopProductDto(g.Key.ProductId, g.Key.ProductName, g.Sum(i => i.Quantity), g.Sum(i => i.Total)))
            .OrderByDescending(p => p.TotalQuantity)
            .ThenBy(p => p.ProductName)
            .Take(TopProductsCount)
            .ToList();

        return new SupplierSummaryDto(
            supplier.Id,
            supplier.Name,
            purchases.Count,
            purchases.Sum(p => p.TotalAmount),
            purchases.Count == 0 ? null : purchases.Max(p => p.PurchaseDate),
            topProducts);
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierDto dto)
    {
        await createValidator.ValidateAndThrowAsync(dto);

        var cnpj = NormalizeCnpj(dto.Cnpj);
        await EnsureCnpjIsAvailableAsync(cnpj, ignoreId: null);

        var supplier = new Supplier();
        ApplyInput(supplier, dto, cnpj);
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();

        return supplier.ToDto();
    }

    public async Task<SupplierDto> UpdateAsync(int id, UpdateSupplierDto dto)
    {
        await updateValidator.ValidateAndThrowAsync(dto);

        var supplier = await FindAsync(id);
        var cnpj = NormalizeCnpj(dto.Cnpj);
        await EnsureCnpjIsAvailableAsync(cnpj, ignoreId: id);

        ApplyInput(supplier, dto, cnpj);
        supplier.Active = dto.Active;
        await context.SaveChangesAsync();

        return supplier.ToDto();
    }

    public async Task DeactivateAsync(int id)
    {
        var supplier = await FindAsync(id);

        // Soft delete: as compras antigas continuam apontando para ele.
        supplier.Active = false;
        await context.SaveChangesAsync();
    }

    private static void ApplyInput(Supplier supplier, ISupplierInput input, string? cnpj)
    {
        supplier.Name = input.Name.Trim();
        supplier.CorporateName = Text.EmptyToNull(input.CorporateName);
        supplier.Cnpj = cnpj;
        supplier.Phone = Text.EmptyToNull(input.Phone);
        supplier.Email = Text.EmptyToNull(input.Email);
        supplier.Address = Text.EmptyToNull(input.Address);
    }


    /// <summary>Grava sempre do mesmo jeito (sem máscara, maiúsculas), para o índice único funcionar.</summary>
    private static string? NormalizeCnpj(string? cnpj) => string.IsNullOrWhiteSpace(cnpj) ? null : Cnpj.Normalize(cnpj);

    private async Task<Supplier> FindAsync(int id) =>
        await context.Suppliers.FindAsync(id)
        ?? throw new NotFoundException("Fornecedor não encontrado.");

    private async Task EnsureCnpjIsAvailableAsync(string? cnpj, int? ignoreId)
    {
        if (cnpj is null)
        {
            return;
        }

        var exists = await context.Suppliers.AnyAsync(s => s.Cnpj == cnpj && (ignoreId == null || s.Id != ignoreId));
        if (exists)
        {
            throw new BusinessException("Já existe um fornecedor com esse CNPJ.");
        }
    }
}
