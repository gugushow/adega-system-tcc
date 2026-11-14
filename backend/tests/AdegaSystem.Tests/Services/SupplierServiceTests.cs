using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Purchases;
using AdegaSystem.Api.DTOs.Suppliers;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;

namespace AdegaSystem.Tests.Services;

public class SupplierServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private SupplierService CreateService() =>
        new(_database.CreateContext(), new CreateSupplierDtoValidator(), new UpdateSupplierDtoValidator());

    private static CreateSupplierDto NewSupplier(string name = "Distribuidora Boa Bebida", string? cnpj = "11.222.333/0001-81") =>
        new(name, "Boa Bebida Comércio Ltda", cnpj, "(11) 99999-0000", "vendas@boabebida.com.br", "Rua das Uvas, 100");

    private async Task CreatePurchaseAsync(int supplierId, DateOnly date, params CreatePurchaseItemDto[] items)
    {
        await using var context = _database.CreateContext();
        var currentUser = new TestData.FakeCurrentUser();
        var stockService = new StockService(context, currentUser, TestData.CreateAuditService(context), new StockAdjustmentDtoValidator());
        await new PurchaseService(context, currentUser, stockService, new CreatePurchaseDtoValidator())
            .CreateAsync(new CreatePurchaseDto(supplierId, null, date, items));
    }

    [Fact]
    public async Task Create_StoresCnpjWithoutMask()
    {
        var created = await CreateService().CreateAsync(NewSupplier());

        Assert.Equal("11222333000181", created.Cnpj);
        Assert.True(created.Active);
    }

    [Fact]
    public async Task Create_AcceptsAlphanumericCnpj()
    {
        var created = await CreateService().CreateAsync(NewSupplier(cnpj: "12.abc.345/01de-35"));

        Assert.Equal("12ABC34501DE35", created.Cnpj);
    }

    [Fact]
    public async Task Create_WithInvalidCnpj_ThrowsValidationException()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().CreateAsync(NewSupplier(cnpj: "11.222.333/0001-82")));

        Assert.Contains(exception.Errors, e => e.PropertyName == "Cnpj");
    }

    [Fact]
    public async Task Create_WithoutCnpj_IsAllowed()
    {
        await CreateService().CreateAsync(NewSupplier(name: "Feira do produtor", cnpj: null));
        var second = await CreateService().CreateAsync(NewSupplier(name: "Mercado do bairro", cnpj: " "));

        Assert.Null(second.Cnpj);
    }

    [Fact]
    public async Task Create_WithDuplicateCnpj_Throws()
    {
        await CreateService().CreateAsync(NewSupplier());

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CreateAsync(NewSupplier(name: "Outro nome", cnpj: "11222333000181")));
        Assert.Equal("Já existe um fornecedor com esse CNPJ.", exception.Message);
    }

    [Fact]
    public async Task Update_WithCnpjOfAnotherSupplier_Throws()
    {
        await CreateService().CreateAsync(NewSupplier());
        var other = await CreateService().CreateAsync(NewSupplier(name: "Outra distribuidora", cnpj: null));

        // Mesmo CNPJ do primeiro fornecedor, só que digitado com máscara.
        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService().UpdateAsync(other.Id,
            new UpdateSupplierDto("Outra distribuidora", null, "11.222.333/0001-81", null, null, null, true)));

        Assert.Equal("Já existe um fornecedor com esse CNPJ.", exception.Message);
        Assert.Null((await CreateService().GetByIdAsync(other.Id)).Cnpj);
    }

    [Fact]
    public async Task Deactivate_KeepsSupplierButMarksInactive()
    {
        var created = await CreateService().CreateAsync(NewSupplier());

        await CreateService().DeactivateAsync(created.Id);

        Assert.False((await CreateService().GetByIdAsync(created.Id)).Active);
    }

    [Fact]
    public async Task GetSummary_TotalsPurchasesAndRanksProducts()
    {
        var supplier = await CreateService().CreateAsync(NewSupplier());
        var heineken = await TestData.AddProductAsync(_database);
        var vinho = await TestData.AddProductAsync(_database, name: "Vinho Tinto");
        await CreatePurchaseAsync(supplier.Id, new DateOnly(2026, 9, 1),
            new CreatePurchaseItemDto(heineken.Id, 24, 3.90m),
            new CreatePurchaseItemDto(vinho.Id, 6, 25.00m));
        await CreatePurchaseAsync(supplier.Id, new DateOnly(2026, 9, 20),
            new CreatePurchaseItemDto(heineken.Id, 12, 4.10m));

        var summary = await CreateService().GetSummaryAsync(supplier.Id);

        Assert.Equal(2, summary.PurchaseCount);
        // 24 x 3,90 + 6 x 25,00 + 12 x 4,10 = 93,60 + 150,00 + 49,20
        Assert.Equal(292.80m, summary.TotalPurchased);
        Assert.Equal(new DateOnly(2026, 9, 20), summary.LastPurchaseDate);
        Assert.Equal(["Heineken 350ml", "Vinho Tinto"], summary.TopProducts.Select(p => p.ProductName));
        Assert.Equal(36, summary.TopProducts[0].TotalQuantity);
    }

    [Fact]
    public async Task GetSummary_WithoutPurchases_ReturnsZeros()
    {
        var supplier = await CreateService().CreateAsync(NewSupplier());

        var summary = await CreateService().GetSummaryAsync(supplier.Id);

        Assert.Equal(0, summary.PurchaseCount);
        Assert.Equal(0m, summary.TotalPurchased);
        Assert.Null(summary.LastPurchaseDate);
        Assert.Empty(summary.TopProducts);
    }

    public void Dispose() => _database.Dispose();
}
