using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Products;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;

namespace AdegaSystem.Tests.Services;

public class ProductServiceTests : IDisposable
{
    // Ids das categorias iniciais criadas pelo seed (ver CategorySeed).
    private const int Cerveja = 1;
    private const int Vinho = 2;

    private readonly TestDatabase _database = new();

    private ProductService CreateService() =>
        CreateService(_database.CreateContext());

    private static ProductService CreateService(AdegaDbContext context) =>
        new(context, TestData.CreateAuditService(context), new CreateProductDtoValidator(), new UpdateProductDtoValidator());

    private static CreateProductDto NewProduct(
        string name = "Heineken 350ml",
        string? barcode = "7896045504404",
        int categoryId = Cerveja,
        decimal costPrice = 3.90m,
        decimal salePrice = 6.50m,
        int minimumStock = 10) =>
        new(name, null, barcode, "Heineken", categoryId, "350ml", 5.0m, costPrice, salePrice, minimumStock);

    private static UpdateProductDto ToUpdate(CreateProductDto dto, bool active = true) =>
        new(dto.Name, dto.Description, dto.Barcode, dto.Brand, dto.CategoryId, dto.Volume,
            dto.AlcoholContent, dto.CostPrice, dto.SalePrice, dto.MinimumStock, active);

    /// <summary>
    /// CurrentStock não tem setter público: nenhum código da aplicação muda o estoque
    /// fora da lógica centralizada de estoque (Fase 2). Nos testes, usamos o EF diretamente
    /// para simular um estoque já existente.
    /// </summary>
    private async Task SetStockAsync(int productId, int stock)
    {
        await using var context = _database.CreateContext();
        var product = await context.Products.FindAsync(productId);
        context.Entry(product!).Property(p => p.CurrentStock).CurrentValue = stock;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_StartsWithZeroStockAndMarkedAsOutOfStock()
    {
        var created = await CreateService().CreateAsync(NewProduct());

        Assert.Equal(0, created.CurrentStock);
        Assert.True(created.IsOutOfStock);
        Assert.True(created.IsLowStock);
        Assert.Equal("Cerveja", created.CategoryName);
    }

    [Fact]
    public async Task Create_CalculatesMarginPercent()
    {
        var created = await CreateService().CreateAsync(NewProduct(costPrice: 8.00m, salePrice: 8.50m));

        // ((8,50 - 8,00) / 8,50) * 100 = 5,88%
        Assert.Equal(5.88m, created.MarginPercent);
    }

    [Fact]
    public async Task Create_WithMissingCategory_Throws()
    {
        await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CreateAsync(NewProduct(categoryId: 9999)));
    }

    [Fact]
    public async Task Create_InInactiveCategory_Throws()
    {
        await using (var context = _database.CreateContext())
        {
            var category = await context.Categories.FindAsync([Vinho], TestContext.Current.CancellationToken);
            category!.Active = false;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CreateAsync(NewProduct(categoryId: Vinho)));
        Assert.Equal("A categoria informada está inativa.", exception.Message);
    }

    [Fact]
    public async Task Create_WithDuplicateBarcode_Throws()
    {
        await CreateService().CreateAsync(NewProduct());

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CreateAsync(NewProduct(name: "Outra cerveja")));
        Assert.Equal("Já existe um produto com esse código de barras.", exception.Message);
    }

    [Fact]
    public async Task Create_WithBlankBarcode_StoresNullAndAllowsMany()
    {
        var first = await CreateService().CreateAsync(NewProduct(name: "Gelo 5kg", barcode: "  "));
        var second = await CreateService().CreateAsync(NewProduct(name: "Gelo 10kg", barcode: null));

        Assert.Null(first.Barcode);
        Assert.Null(second.Barcode);
    }

    [Fact]
    public async Task Create_WithBlankOptionalTexts_StoresNull()
    {
        // Descrição, marca e volume só com espaços contam como "não informado" (ver Text.EmptyToNull).
        var created = await CreateService().CreateAsync(
            NewProduct() with { Description = "  ", Brand = "  ", Volume = "  " });

        Assert.Null(created.Description);
        Assert.Null(created.Brand);
        Assert.Null(created.Volume);
    }

    [Fact]
    public async Task Update_WithBlankOptionalTexts_StoresNull()
    {
        var created = await CreateService().CreateAsync(NewProduct() with { Description = "Cerveja puro malte" });

        var updated = await CreateService().UpdateAsync(created.Id,
            ToUpdate(NewProduct() with { Description = "  ", Brand = "  ", Volume = "  " }));

        Assert.Null(updated.Description);
        Assert.Null(updated.Brand);
        Assert.Null(updated.Volume);
    }

    [Fact]
    public async Task Create_WithNegativeSalePrice_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().CreateAsync(NewProduct(salePrice: -1)));
    }

    [Fact]
    public async Task Update_ChangesDataButNeverTheStock()
    {
        var created = await CreateService().CreateAsync(NewProduct());
        await SetStockAsync(created.Id, 50);

        var updated = await CreateService().UpdateAsync(created.Id, ToUpdate(NewProduct(salePrice: 7.00m)));

        Assert.Equal(7.00m, updated.SalePrice);
        Assert.Equal(50, updated.CurrentStock);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_WithBarcodeOfAnotherProduct_Throws()
    {
        await CreateService().CreateAsync(NewProduct(barcode: "111"));
        var other = await CreateService().CreateAsync(NewProduct(name: "Amstel", barcode: "222"));

        await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().UpdateAsync(other.Id, ToUpdate(NewProduct(name: "Amstel", barcode: "111"))));
    }

    [Fact]
    public async Task GetAll_LowStockFilter_ReturnsProductsAtOrBelowMinimum()
    {
        var atMinimum = await CreateService().CreateAsync(NewProduct(name: "No mínimo", barcode: "1", minimumStock: 10));
        var above = await CreateService().CreateAsync(NewProduct(name: "Acima", barcode: "2", minimumStock: 10));
        await SetStockAsync(atMinimum.Id, 10);
        await SetStockAsync(above.Id, 11);

        var result = await CreateService().GetAllAsync(new ProductQuery { Stock = StockFilter.Low });

        Assert.Contains(result.Items, p => p.Id == atMinimum.Id);
        Assert.DoesNotContain(result.Items, p => p.Id == above.Id);
    }

    [Fact]
    public async Task GetAll_OutOfStockFilter_ReturnsOnlyZeroStock()
    {
        var empty = await CreateService().CreateAsync(NewProduct(name: "Zerado", barcode: "1"));
        var withStock = await CreateService().CreateAsync(NewProduct(name: "Com estoque", barcode: "2"));
        await SetStockAsync(withStock.Id, 3);

        var result = await CreateService().GetAllAsync(new ProductQuery { Stock = StockFilter.Out });

        Assert.Equal([empty.Id], result.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task GetAll_FiltersBySearchAndCategoryWithPagination()
    {
        await CreateService().CreateAsync(NewProduct(name: "Heineken 350ml", barcode: "1"));
        await CreateService().CreateAsync(NewProduct(name: "Heineken 600ml", barcode: "2"));
        await CreateService().CreateAsync(NewProduct(name: "Heineken Long Neck", barcode: "3"));
        await CreateService().CreateAsync(NewProduct(name: "Vinho Heineken?", barcode: "4", categoryId: Vinho));

        var result = await CreateService().GetAllAsync(
            new ProductQuery { Search = "heineken", CategoryId = Cerveja, Page = 1, PageSize = 2 });

        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(["Heineken 350ml", "Heineken 600ml"], result.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task GetByBarcode_ReturnsProductOrThrowsNotFound()
    {
        var created = await CreateService().CreateAsync(NewProduct(barcode: "7891234567890"));

        var found = await CreateService().GetByBarcodeAsync("7891234567890");

        Assert.Equal(created.Id, found.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetByBarcodeAsync("000"));
    }

    [Fact]
    public async Task GetByBarcode_IgnoresSurroundingSpaces()
    {
        var created = await CreateService().CreateAsync(NewProduct(barcode: "7891234567890"));

        var found = await CreateService().GetByBarcodeAsync("  7891234567890 ");

        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task GetByBarcode_ReturnsInactiveProductToo()
    {
        // Quem decide o que fazer com um produto inativo é a tela (o PDV não deixa vender).
        var created = await CreateService().CreateAsync(NewProduct(barcode: "7891234567890"));
        await CreateService().DeactivateAsync(created.Id);

        var found = await CreateService().GetByBarcodeAsync("7891234567890");

        Assert.False(found.Active);
    }

    [Theory]
    [InlineData("036000291452", "0036000291452")]  // cadastrado como UPC-A, lido como EAN-13
    [InlineData("0036000291452", "036000291452")]  // cadastrado como EAN-13, lido como UPC-A
    public async Task GetByBarcode_FindsEquivalentUpcAAndEan13(string stored, string scanned)
    {
        var created = await CreateService().CreateAsync(NewProduct(barcode: stored));

        var found = await CreateService().GetByBarcodeAsync(scanned);

        Assert.Equal(created.Id, found.Id);
        Assert.Equal(stored, found.Barcode); // o código salvo não é reescrito
    }

    [Fact]
    public async Task GetByBarcode_PrefersExactMatchOverEquivalent()
    {
        // Caso antigo (anterior à regra de equivalência): cada forma do mesmo GTIN num produto.
        // Cada leitura acha exatamente o produto com aquele código; nunca mistura os dois.
        var upc = await CreateService().CreateAsync(NewProduct(name: "Produto UPC", barcode: "036000291452"));
        int eanId;
        await using (var context = _database.CreateContext())
        {
            var ean = new Product
            {
                Name = "Produto EAN", Barcode = "0036000291452", CategoryId = Cerveja, CostPrice = 1, SalePrice = 2,
            };
            context.Products.Add(ean);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            eanId = ean.Id;
        }

        Assert.Equal(upc.Id, (await CreateService().GetByBarcodeAsync("036000291452")).Id);
        Assert.Equal(eanId, (await CreateService().GetByBarcodeAsync("0036000291452")).Id);
    }

    [Theory]
    [InlineData("036000291453", "0036000291453")]  // dígito verificador errado: não há equivalência
    [InlineData("12345678", "012345678")]          // tamanhos que não são UPC-A/EAN-13
    [InlineData("1036000291452", "036000291452")]  // EAN-13 que não começa com 0 não vira UPC-A
    public async Task GetByBarcode_DoesNotUseEquivalenceWhenItIsNotValid(string stored, string scanned)
    {
        await CreateService().CreateAsync(NewProduct(barcode: stored));

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetByBarcodeAsync(scanned));
    }

    [Fact]
    public async Task Create_WithEquivalentBarcodeOfAnotherProduct_Throws()
    {
        await CreateService().CreateAsync(NewProduct(barcode: "036000291452"));

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CreateAsync(NewProduct(name: "Outra", barcode: "0036000291452")));
        Assert.Equal("Já existe um produto com o código 036000291452, que é o mesmo código de barras (UPC-A/EAN-13).",
            exception.Message);
    }

    [Fact]
    public async Task Update_KeepingItsOwnBarcode_Succeeds()
    {
        var created = await CreateService().CreateAsync(NewProduct(barcode: "7891234567890"));

        var updated = await CreateService().UpdateAsync(created.Id, ToUpdate(NewProduct(barcode: "7891234567890", salePrice: 9m)));

        Assert.Equal("7891234567890", updated.Barcode);
        Assert.Equal(9m, updated.SalePrice);
    }

    [Fact]
    public async Task Update_SwitchingToTheEquivalentFormOfItsOwnBarcode_Succeeds()
    {
        var created = await CreateService().CreateAsync(NewProduct(barcode: "036000291452"));

        var updated = await CreateService().UpdateAsync(created.Id, ToUpdate(NewProduct(barcode: "0036000291452")));

        Assert.Equal("0036000291452", updated.Barcode);
    }

    [Fact]
    public async Task Update_ToEquivalentBarcodeOfAnotherProduct_Throws()
    {
        await CreateService().CreateAsync(NewProduct(barcode: "036000291452"));
        var other = await CreateService().CreateAsync(NewProduct(name: "Amstel", barcode: "222"));

        await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().UpdateAsync(other.Id, ToUpdate(NewProduct(name: "Amstel", barcode: "0036000291452"))));
    }

    [Fact]
    public async Task Deactivate_KeepsRecordAsInactive()
    {
        var created = await CreateService().CreateAsync(NewProduct());

        await CreateService().DeactivateAsync(created.Id);

        var reloaded = await CreateService().GetByIdAsync(created.Id);
        Assert.False(reloaded.Active);
    }

    public void Dispose() => _database.Dispose();
}
