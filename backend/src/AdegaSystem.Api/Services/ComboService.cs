using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Combos;
using AdegaSystem.Api.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Cadastro de combos. O combo só guarda "quais produtos e quantos de cada" e o preço fechado;
/// a venda (SaleService) é que abre o combo em itens e dá baixa produto por produto.
/// </summary>
public class ComboService(
    AdegaDbContext context,
    IAuditService auditService,
    IValidator<CreateComboDto> createValidator,
    IValidator<UpdateComboDto> updateValidator) : IComboService
{
    public async Task<PagedResult<ComboDto>> GetAllAsync(ComboQuery query)
    {
        var combos = context.Combos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            combos = combos.Where(c => c.Name.ToLower().Contains(search));
        }

        if (query.Active.HasValue)
        {
            combos = combos.Where(c => c.Active == query.Active.Value);
        }

        var totalItems = await combos.CountAsync();
        var page = await combos
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .AsSplitQuery()
            .ToListAsync();

        return new PagedResult<ComboDto>(page.Select(ToDto).ToList(), query.Page, query.PageSize, totalItems);
    }

    public async Task<ComboDto> GetByIdAsync(int id)
    {
        var combo = await context.Combos.AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Combo não encontrado.");

        return ToDto(combo);
    }

    public async Task<ComboDto> CreateAsync(CreateComboDto dto)
    {
        await createValidator.ValidateAndThrowAsync(dto);
        var products = await LoadProductsAsync(dto.Items, requireActive: true);
        EnsurePriceIsNotAboveNormal(dto.SalePrice, dto.Items, products);

        var combo = new Combo
        {
            Name = dto.Name.Trim(),
            Description = Text.EmptyToNull(dto.Description),
            SalePrice = dto.SalePrice,
            Items = dto.Items.Select(i => new ComboItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList(),
        };
        // A auditoria precisa do Id gerado (dois SaveChanges), por isso a transação mantém os dois juntos.
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Combos.Add(combo);
        await context.SaveChangesAsync();

        auditService.Add(AuditAction.COMBO_CREATED, nameof(Combo), combo.Id, combo.Name, newValue: Snapshot(combo, products));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetByIdAsync(combo.Id);
    }

    public async Task<ComboDto> UpdateAsync(int id, UpdateComboDto dto)
    {
        await updateValidator.ValidateAndThrowAsync(dto);

        var combo = await context.Combos
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Combo não encontrado.");

        // Um combo inativo pode ficar com produto desativado; para (re)ativar, todos precisam estar ativos.
        var products = await LoadProductsAsync(dto.Items, requireActive: dto.Active);
        EnsurePriceIsNotAboveNormal(dto.SalePrice, dto.Items, products);

        var before = Snapshot(combo, combo.Items.ToDictionary(i => i.ProductId, i => i.Product));

        combo.Name = dto.Name.Trim();
        combo.Description = Text.EmptyToNull(dto.Description);
        combo.SalePrice = dto.SalePrice;
        combo.Active = dto.Active;
        combo.UpdatedAt = DateTime.UtcNow;

        // Os itens enviados substituem os atuais. Vendas antigas não mudam: elas guardaram
        // os próprios itens (produto, quantidade e preço) no momento da venda.
        context.ComboItems.RemoveRange(combo.Items);
        combo.Items = dto.Items.Select(i => new ComboItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList();

        auditService.Add(AuditAction.COMBO_UPDATED, nameof(Combo), combo.Id, combo.Name, before, Snapshot(combo, products));
        await context.SaveChangesAsync();

        return await GetByIdAsync(combo.Id);
    }

    public async Task DeactivateAsync(int id)
    {
        var combo = await context.Combos.FindAsync(id)
            ?? throw new NotFoundException("Combo não encontrado.");

        if (!combo.Active)
        {
            return;
        }

        // Soft delete: vendas antigas continuam apontando para o combo.
        combo.Active = false;
        combo.UpdatedAt = DateTime.UtcNow;
        auditService.Add(AuditAction.COMBO_DEACTIVATED, nameof(Combo), combo.Id, combo.Name, new { active = true }, new { active = false });
        await context.SaveChangesAsync();
    }

    /// <summary>Carrega os produtos dos itens e confere se existem (e, se pedido, se estão ativos).</summary>
    private async Task<Dictionary<int, Product>> LoadProductsAsync(IReadOnlyList<ComboItemInputDto> items, bool requireActive)
    {
        var ids = items.Select(i => i.ProductId).ToList();
        var products = await context.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new BusinessException($"O produto de código {item.ProductId} não existe.");
            }

            if (requireActive && !product.Active)
            {
                throw new BusinessException($"O produto \"{product.Name}\" está inativo e não pode fazer parte de um combo ativo.");
            }
        }

        return products;
    }

    /// <summary>
    /// Combo mais caro que os produtos separados não faz sentido (e daria "desconto negativo" na venda).
    /// Igual é permitido: às vezes o combo é só uma forma prática de vender junto.
    /// </summary>
    private static void EnsurePriceIsNotAboveNormal(decimal salePrice, IReadOnlyList<ComboItemInputDto> items, Dictionary<int, Product> products)
    {
        var normalPrice = items.Sum(i => i.Quantity * products[i.ProductId].SalePrice);
        if (salePrice > normalPrice)
        {
            throw new BusinessException(
                $"O preço do combo ({Money.Format(salePrice)}) não pode ser maior que a soma dos produtos separados ({Money.Format(normalPrice)}).");
        }
    }

    /// <summary>O que vai para a auditoria: nome, preço e a lista "2x Heineken 350ml".</summary>
    private static object Snapshot(Combo combo, IReadOnlyDictionary<int, Product> products) => new
    {
        name = combo.Name,
        salePrice = combo.SalePrice,
        active = combo.Active,
        items = combo.Items.Select(i => $"{i.Quantity}x {products[i.ProductId].Name}").ToList(),
    };

    private static ComboDto ToDto(Combo combo)
    {
        var items = combo.Items
            .OrderBy(i => i.Product.Name)
            .Select(i => new ComboItemDto(i.ProductId, i.Product.Name, i.Quantity, i.Product.SalePrice, i.Product.CurrentStock, i.Product.Active))
            .ToList();

        var normalPrice = items.Sum(i => i.Quantity * i.UnitPrice);
        var savings = normalPrice - combo.SalePrice;
        // O produto mais escasso decide: 10 Red Bull com 4 por combo dão só 2 combos.
        var available = items.Count == 0 ? 0 : items.Min(i => Math.Max(0, i.CurrentStock) / i.Quantity);

        return new ComboDto(
            combo.Id,
            combo.Name,
            combo.Description,
            combo.SalePrice,
            normalPrice,
            savings,
            normalPrice == 0 ? 0 : Math.Round(savings / normalPrice * 100, 2, MidpointRounding.AwayFromZero),
            available,
            combo.Active,
            combo.CreatedAt,
            combo.UpdatedAt,
            items);
    }
}
