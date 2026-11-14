using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Products;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Mappings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

public class ProductService(
    AdegaDbContext context,
    IAuditService auditService,
    IValidator<CreateProductDto> createValidator,
    IValidator<UpdateProductDto> updateValidator) : IProductService
{
    public async Task<PagedResult<ProductDto>> GetAllAsync(ProductQuery query)
    {
        // A consulta é montada aos poucos. Nada vai ao banco até o CountAsync/ToListAsync.
        var products = context.Products.AsNoTracking().Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // ToLower dos dois lados deixa a busca igual no MySQL e no SQLite dos testes, sem depender da collation.
            var search = query.Search.Trim().ToLower();
            products = products.Where(p =>
                p.Name.ToLower().Contains(search) ||
                (p.Brand != null && p.Brand.ToLower().Contains(search)) ||
                p.Barcode == query.Search.Trim());
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.Active.HasValue)
        {
            products = products.Where(p => p.Active == query.Active.Value);
        }

        // Nos filtros de estoque escrevemos a condição por extenso, porque IsLowStock
        // é uma propriedade C# que o EF não sabe traduzir para SQL.
        products = query.Stock switch
        {
            StockFilter.Low => products.Where(p => p.Active && p.CurrentStock <= p.MinimumStock),
            StockFilter.Out => products.Where(p => p.Active && p.CurrentStock == 0),
            _ => products,
        };

        var totalItems = await products.CountAsync();

        var page = await products
            .OrderBy(p => p.Name)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<ProductDto>(page.Select(p => p.ToDto()).ToList(), query.Page, query.PageSize, totalItems);
    }

    public async Task<ProductDto> GetByIdAsync(int id)
    {
        var product = await context.Products.AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Produto não encontrado.");

        return product.ToDto();
    }

    public async Task<ProductDto> GetByBarcodeAsync(string barcode)
    {
        var code = barcode.Trim();
        var products = context.Products.AsNoTracking().Include(p => p.Category);

        // Primeiro o valor exato. Só se não houver, tenta a forma equivalente UPC-A ⇄ EAN-13 (ver Gtin).
        // O índice único garante no máximo um produto por forma, e o exato sempre ganha: dois produtos
        // diferentes nunca são confundidos.
        var product = await products.FirstOrDefaultAsync(p => p.Barcode == code);
        var equivalent = Gtin.Equivalent(code);
        if (product is null && equivalent is not null)
        {
            product = await products.FirstOrDefaultAsync(p => p.Barcode == equivalent);
        }

        return product?.ToDto() ?? throw new NotFoundException("Nenhum produto com esse código de barras.");
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        await createValidator.ValidateAndThrowAsync(dto);

        var barcode = NormalizeBarcode(dto.Barcode);
        await EnsureCategoryIsActiveAsync(dto.CategoryId);
        await EnsureBarcodeIsAvailableAsync(barcode, ignoreId: null);

        // CurrentStock não é preenchido aqui: todo produto nasce com estoque 0
        // e só recebe unidades por uma compra ou ajuste (Fase 2).
        var product = new Product();
        ApplyInput(product, dto, barcode);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return await GetByIdAsync(product.Id);
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductDto dto)
    {
        await updateValidator.ValidateAndThrowAsync(dto);

        var product = await FindAsync(id);
        var barcode = NormalizeBarcode(dto.Barcode);

        // Um produto ativo precisa estar numa categoria ativa.
        if (dto.Active)
        {
            await EnsureCategoryIsActiveAsync(dto.CategoryId);
        }
        await EnsureBarcodeIsAvailableAsync(barcode, ignoreId: id, checkEquivalent: barcode != product.Barcode);

        var before = new { product.CostPrice, product.SalePrice, product.Active };

        ApplyInput(product, dto, barcode);
        product.Active = dto.Active;
        product.UpdatedAt = DateTime.UtcNow;
        AuditChanges(product, before.CostPrice, before.SalePrice, before.Active);
        await context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task DeactivateAsync(int id)
    {
        var product = await FindAsync(id);

        if (!product.Active)
        {
            return;
        }

        // Soft delete: o produto some das telas de venda, mas continua no banco
        // porque vendas, compras e movimentações antigas apontam para ele.
        product.Active = false;
        product.UpdatedAt = DateTime.UtcNow;
        AuditChanges(product, product.CostPrice, product.SalePrice, wasActive: true);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Auditoria das mudanças importantes do cadastro: preço (custo ou venda) e ativação.
    /// Nome, marca ou descrição não entram: não mudam dinheiro nem estoque.
    /// </summary>
    private void AuditChanges(Product product, decimal oldCost, decimal oldSale, bool wasActive)
    {
        if (product.CostPrice != oldCost || product.SalePrice != oldSale)
        {
            auditService.Add(AuditAction.PRODUCT_PRICE_CHANGED, nameof(Product), product.Id, product.Name,
                new { costPrice = oldCost, salePrice = oldSale },
                new { costPrice = product.CostPrice, salePrice = product.SalePrice });
        }

        if (product.Active != wasActive)
        {
            auditService.Add(product.Active ? AuditAction.PRODUCT_REACTIVATED : AuditAction.PRODUCT_DEACTIVATED,
                nameof(Product), product.Id, product.Name, new { active = wasActive }, new { active = product.Active });
        }
    }

    /// <summary>Copia os campos editáveis da DTO para a entidade. CurrentStock fica de fora.</summary>
    private static void ApplyInput(Product product, IProductInput input, string? barcode)
    {
        product.Name = input.Name.Trim();
        product.Description = Text.EmptyToNull(input.Description);
        product.Barcode = barcode;
        product.Brand = Text.EmptyToNull(input.Brand);
        product.CategoryId = input.CategoryId;
        product.Volume = Text.EmptyToNull(input.Volume);
        product.AlcoholContent = input.AlcoholContent;
        product.CostPrice = input.CostPrice;
        product.SalePrice = input.SalePrice;
        product.MinimumStock = input.MinimumStock;
    }

    /// <summary>"" e "   " viram null, para não colidirem no índice único.</summary>
    private static string? NormalizeBarcode(string? barcode) =>
        string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();

    private async Task<Product> FindAsync(int id) =>
        await context.Products.FindAsync(id)
        ?? throw new NotFoundException("Produto não encontrado.");

    private async Task EnsureCategoryIsActiveAsync(int categoryId)
    {
        var category = await context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId)
            ?? throw new BusinessException("A categoria informada não existe.");

        if (!category.Active)
        {
            throw new BusinessException("A categoria informada está inativa.");
        }
    }

    /// <param name="checkEquivalent">
    /// Também recusa a forma equivalente UPC-A ⇄ EAN-13 (ver Gtin) de outro produto. Só é ligado quando
    /// o código é novo ou mudou: se o banco já tiver um caso antigo assim, editar outros campos do
    /// produto continua funcionando.
    /// </param>
    private async Task EnsureBarcodeIsAvailableAsync(string? barcode, int? ignoreId, bool checkEquivalent = true)
    {
        if (barcode is null)
        {
            return;
        }

        var exists = await context.Products.AnyAsync(p => p.Barcode == barcode && (ignoreId == null || p.Id != ignoreId));
        if (exists)
        {
            throw new BusinessException("Já existe um produto com esse código de barras.");
        }

        var equivalent = checkEquivalent ? Gtin.Equivalent(barcode) : null;
        if (equivalent is not null &&
            await context.Products.AnyAsync(p => p.Barcode == equivalent && (ignoreId == null || p.Id != ignoreId)))
        {
            throw new BusinessException(
                $"Já existe um produto com o código {equivalent}, que é o mesmo código de barras (UPC-A/EAN-13).");
        }
    }
}
