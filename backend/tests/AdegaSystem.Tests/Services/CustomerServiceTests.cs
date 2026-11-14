using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.DTOs.Customers;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using AdegaSystem.Tests.Infrastructure;
using FluentValidation;

namespace AdegaSystem.Tests.Services;

public class CustomerServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private CustomerService CreateService() =>
        new(_database.CreateContext(), new CreateCustomerDtoValidator(), new UpdateCustomerDtoValidator());

    private static CreateCustomerDto NewCustomer(string name = "Maria Souza", string? cpf = "529.982.247-25") =>
        new(name, cpf, "(11) 98888-7777", "maria@email.com", new DateOnly(1990, 5, 20));

    [Fact]
    public async Task Create_StoresCpfWithoutMask()
    {
        var created = await CreateService().CreateAsync(NewCustomer());

        Assert.Equal("52998224725", created.Cpf);
        Assert.True(created.Active);
        Assert.Equal(new DateOnly(1990, 5, 20), created.BirthDate);
    }

    [Fact]
    public async Task Create_OnlyNameIsRequired()
    {
        // Cadastro rápido no PDV: só o nome.
        var created = await CreateService().CreateAsync(new CreateCustomerDto("Cliente Rápido"));

        Assert.Null(created.Cpf);
        Assert.Null(created.Email);
    }

    [Theory]
    [InlineData("529.982.247-26")] // dígito verificador errado
    [InlineData("111.111.111-11")] // todos iguais
    [InlineData("5299822472")]     // 10 dígitos
    [InlineData("529.982.247-2a")] // letra
    public async Task Create_WithInvalidCpf_ThrowsValidationException(string cpf)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateService().CreateAsync(NewCustomer(cpf: cpf)));

        Assert.Contains(exception.Errors, e => e.PropertyName == "Cpf");
    }

    [Fact]
    public async Task Create_WithDuplicateCpf_Throws()
    {
        await CreateService().CreateAsync(NewCustomer());

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService().CreateAsync(NewCustomer("Outra Maria", "52998224725")));

        Assert.Equal("Já existe um cliente com esse CPF.", exception.Message);
    }

    [Fact]
    public async Task Create_WithBirthDateInTheFuture_ThrowsValidationException()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateService().CreateAsync(
            new CreateCustomerDto("Viajante do Tempo", BirthDate: BusinessClock.Today.AddDays(1))));

        Assert.Contains(exception.Errors, e => e.PropertyName == "BirthDate");
    }

    [Fact]
    public async Task Update_ChangesDataAndKeepsOwnCpf()
    {
        var created = await CreateService().CreateAsync(NewCustomer());

        var updated = await CreateService().UpdateAsync(created.Id,
            new UpdateCustomerDto("Maria S. Lima", "529.982.247-25", null, " ", null, true), canChangeStatus: true);

        Assert.Equal("Maria S. Lima", updated.Name);
        Assert.Null(updated.Phone);
        Assert.Null(updated.Email);
    }

    [Fact]
    public async Task Update_WithCpfOfAnotherCustomer_Throws()
    {
        await CreateService().CreateAsync(NewCustomer());
        var other = await CreateService().CreateAsync(NewCustomer("João Lima", "111.444.777-35"));

        // O CPF da Maria, sem máscara: depois de normalizado é o mesmo número.
        var exception = await Assert.ThrowsAsync<BusinessException>(() => CreateService().UpdateAsync(other.Id,
            new UpdateCustomerDto("João Lima", "52998224725", null, null, null, true), canChangeStatus: true));

        Assert.Equal("Já existe um cliente com esse CPF.", exception.Message);
        Assert.Equal("11144477735", (await CreateService().GetByIdAsync(other.Id)).Cpf);
    }

    [Fact]
    public async Task Update_WithoutPermissionToChangeStatus_KeepsCustomerActive()
    {
        // Vendedor editando: o Active=false da requisição é ignorado, mas os outros dados mudam.
        var created = await CreateService().CreateAsync(NewCustomer());

        var updated = await CreateService().UpdateAsync(created.Id,
            new UpdateCustomerDto("Maria S. Lima", "529.982.247-25", null, null, null, Active: false), canChangeStatus: false);

        Assert.True(updated.Active);
        Assert.Equal("Maria S. Lima", updated.Name);
        Assert.True((await CreateService().GetByIdAsync(created.Id)).Active);
    }

    [Fact]
    public async Task Update_WithPermissionToChangeStatus_DeactivatesCustomer()
    {
        // ADMIN editando: o Active=false é aplicado.
        var created = await CreateService().CreateAsync(NewCustomer());

        var updated = await CreateService().UpdateAsync(created.Id,
            new UpdateCustomerDto("Maria Souza", "529.982.247-25", null, null, null, Active: false), canChangeStatus: true);

        Assert.False(updated.Active);
        Assert.False((await CreateService().GetByIdAsync(created.Id)).Active);
    }

    [Fact]
    public async Task Deactivate_KeepsTheCustomerButInactive()
    {
        var created = await CreateService().CreateAsync(NewCustomer());

        await CreateService().DeactivateAsync(created.Id);

        Assert.False((await CreateService().GetByIdAsync(created.Id)).Active);
    }

    [Fact]
    public async Task GetAll_SearchesByNameCpfOrPhone()
    {
        await CreateService().CreateAsync(NewCustomer("Maria Souza", "529.982.247-25"));
        await CreateService().CreateAsync(new CreateCustomerDto("João Lima", "111.444.777-35", "(21) 97777-1234"));

        var byName = await CreateService().GetAllAsync(new CustomerQuery { Search = "joão" });
        var byCpf = await CreateService().GetAllAsync(new CustomerQuery { Search = "529.982" });
        var byPhone = await CreateService().GetAllAsync(new CustomerQuery { Search = "97777" });

        Assert.Equal("João Lima", Assert.Single(byName.Items).Name);
        Assert.Equal("Maria Souza", Assert.Single(byCpf.Items).Name);
        Assert.Equal("João Lima", Assert.Single(byPhone.Items).Name);
    }

    [Fact]
    public async Task GetSummary_CountsOnlyCompletedSales()
    {
        var customer = await CreateService().CreateAsync(NewCustomer());
        var product = await TestData.AddProductAsync(_database, salePrice: 10.00m);
        await TestData.ReceiveBatchAsync(_database, product.Id, 20, BusinessClock.Today.AddDays(30));

        await SellAsync(customer.Id, product.Id, 2);  // R$ 20,00
        await SellAsync(customer.Id, product.Id, 4);  // R$ 40,00
        var canceled = await SellAsync(customer.Id, product.Id, 5);
        await using (var context = _database.CreateContext())
        {
            await TestData.CreateSaleService(context).CancelAsync(canceled.Id, new CancelSaleDto("Teste"));
        }

        var summary = await CreateService().GetSummaryAsync(customer.Id);

        Assert.Equal(2, summary.PurchaseCount);
        Assert.Equal(60.00m, summary.TotalSpent);
        Assert.Equal(30.00m, summary.AverageTicket);
        Assert.NotNull(summary.LastPurchaseDate);
    }

    [Fact]
    public async Task GetSummary_WithoutSales_ReturnsZeros()
    {
        var customer = await CreateService().CreateAsync(NewCustomer());

        var summary = await CreateService().GetSummaryAsync(customer.Id);

        Assert.Equal(0, summary.PurchaseCount);
        Assert.Equal(0m, summary.AverageTicket);
        Assert.Null(summary.LastPurchaseDate);
    }

    private async Task<SaleDetailsDto> SellAsync(int customerId, int productId, int quantity)
    {
        await using var context = _database.CreateContext();
        return await TestData.CreateSaleService(context).CreateAsync(
            new CreateSaleDto(customerId, PaymentMethod.PIX, 0, [new CreateSaleItemDto(productId, quantity)]));
    }

    public void Dispose() => _database.Dispose();
}
