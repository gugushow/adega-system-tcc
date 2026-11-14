using System.Text.Json.Nodes;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Audit;
using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.DTOs.Batches;
using AdegaSystem.Api.DTOs.Products;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using AdegaSystem.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdegaSystem.Tests.Services;

/// <summary>Cada ação importante gera exatamente um registro de auditoria, com o antes e o depois.</summary>
public class AuditServiceTests : IDisposable
{
    private static readonly DateOnly Today = BusinessClock.Today;

    private readonly TestDatabase _database = new();

    private async Task<List<AuditLog>> LogsAsync(AuditAction action)
    {
        await using var context = _database.CreateContext();
        return await context.AuditLogs.Where(a => a.Action == action).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Lê um número do JSON gravado. Compara o valor, não o texto: o MySQL guarda "3.90" e o SQLite dos testes, "3.9".
    /// </summary>
    private static decimal Number(string? json, string key) => JsonNode.Parse(json!)![key]!.GetValue<decimal>();

    private ProductService CreateProductService()
    {
        var context = _database.CreateContext();
        return new ProductService(context, TestData.CreateAuditService(context), new CreateProductDtoValidator(), new UpdateProductDtoValidator());
    }

    private static UpdateProductDto Edit(Product product, decimal costPrice, decimal salePrice, bool active = true) =>
        new(product.Name, null, null, null, product.CategoryId, null, null, costPrice, salePrice, product.MinimumStock, active);

    [Fact]
    public async Task ProductPriceChange_IsAuditedWithOldAndNewPrices()
    {
        var product = await TestData.AddProductAsync(_database, costPrice: 3.90m, salePrice: 6.50m);

        await CreateProductService().UpdateAsync(product.Id, Edit(product, 3.90m, 6.90m));

        var log = Assert.Single(await LogsAsync(AuditAction.PRODUCT_PRICE_CHANGED));
        Assert.Equal(("Product", product.Id, "Heineken 350ml", TestData.AdminUserId), (log.Entity, log.EntityId, log.Description, log.UserId));
        Assert.Equal((3.90m, 6.50m), (Number(log.OldValue, "costPrice"), Number(log.OldValue, "salePrice")));
        Assert.Equal((3.90m, 6.90m), (Number(log.NewValue, "costPrice"), Number(log.NewValue, "salePrice")));
    }

    [Fact]
    public async Task ProductEditWithoutPriceChange_IsNotAudited()
    {
        var product = await TestData.AddProductAsync(_database, costPrice: 3.90m, salePrice: 6.50m);

        await CreateProductService().UpdateAsync(product.Id, Edit(product, 3.90m, 6.50m));

        await using var context = _database.CreateContext();
        Assert.False(await context.AuditLogs.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProductDeactivation_IsAudited()
    {
        var product = await TestData.AddProductAsync(_database);

        await CreateProductService().DeactivateAsync(product.Id);
        await CreateProductService().DeactivateAsync(product.Id); // já inativo: não registra de novo

        var log = Assert.Single(await LogsAsync(AuditAction.PRODUCT_DEACTIVATED));
        Assert.Equal("{\"active\":false}", log.NewValue);
    }

    [Fact]
    public async Task StockAdjustment_IsAuditedWithPreviousAndNewStock()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 50, Today.AddDays(30), batchNumber: "L-77");

        await TestData.CreateStockService(_database.CreateContext())
            .AdjustAsync(new StockAdjustmentDto(batch.Id, MovementType.LOSS, -5, "Garrafas quebradas"));

        var log = Assert.Single(await LogsAsync(AuditAction.STOCK_ADJUSTED));
        Assert.Equal(("Product", product.Id), (log.Entity, log.EntityId));
        Assert.Equal("{\"stock\":50}", log.OldValue);
        Assert.Equal("{\"stock\":45,\"type\":\"LOSS\",\"batch\":\"L-77\",\"reason\":\"Garrafas quebradas\"}", log.NewValue);
    }

    [Fact]
    public async Task ExpiredBatchWriteOff_IsAudited()
    {
        var product = await TestData.AddProductAsync(_database);
        var batch = await TestData.ReceiveBatchAsync(_database, product.Id, 4, Today.AddDays(-1), batchNumber: "L-OLD");

        var context = _database.CreateContext();
        await new BatchService(context, TestData.CreateStockService(context), TestData.CreateAuditService(context))
            .WriteOffAsync(batch.Id, new WriteOffBatchDto(null));

        var log = Assert.Single(await LogsAsync(AuditAction.BATCH_WRITTEN_OFF));
        Assert.Equal(("ProductBatch", batch.Id, "Heineken 350ml · lote L-OLD"), (log.Entity, log.EntityId, log.Description));
        Assert.Contains("\"batchQuantity\":4", log.OldValue);
        Assert.Contains("\"batchQuantity\":0", log.NewValue);
    }

    [Fact]
    public async Task SaleCancel_IsAudited()
    {
        var product = await TestData.AddProductAsync(_database);
        await TestData.ReceiveBatchAsync(_database, product.Id, 10, Today.AddDays(30));
        var sale = await TestData.CreateSaleService(_database.CreateContext())
            .CreateAsync(new CreateSaleDto(null, PaymentMethod.CASH, 0, [new CreateSaleItemDto(product.Id, 2)]));

        await TestData.CreateSaleService(_database.CreateContext()).CancelAsync(sale.Id, new CancelSaleDto("Erro no caixa"));

        var log = Assert.Single(await LogsAsync(AuditAction.SALE_CANCELED));
        Assert.Equal(("Sale", sale.Id, $"Venda #{sale.Id}"), (log.Entity, log.EntityId, log.Description));
        Assert.Contains("\"status\":\"COMPLETED\"", log.OldValue);
        Assert.Equal(13m, Number(log.OldValue, "total"));
        Assert.Equal("{\"status\":\"CANCELED\",\"reason\":\"Erro no caixa\"}", log.NewValue);
    }

    [Fact]
    public async Task UserPasswordReset_IsAudited_WithoutThePassword()
    {
        var user = await TestData.AddUserAsync(_database, "ana@adega.com", UserRole.VENDEDOR, name: "Ana");
        var context = _database.CreateContext();
        var service = new UserService(context, new PasswordHasher<User>(), new TestData.FakeCurrentUser(), TestData.CreateAuditService(context),
            new CreateUserDtoValidator(), new UpdateUserDtoValidator());

        await service.UpdateAsync(user.Id, new UpdateUserDto("Ana", "ana@adega.com", UserRole.ESTOQUISTA, true, "NovaSenha123"));

        var log = Assert.Single(await LogsAsync(AuditAction.USER_UPDATED));
        Assert.Contains("\"role\":\"VENDEDOR\"", log.OldValue);
        Assert.Contains("\"role\":\"ESTOQUISTA\"", log.NewValue);
        Assert.Contains("\"passwordReset\":true", log.NewValue);
        Assert.DoesNotContain("NovaSenha123", log.NewValue);
        Assert.DoesNotContain("hash", log.NewValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OwnPasswordChange_IsAudited_AsTheUserThemselves()
    {
        var user = await TestData.AddUserAsync(_database, "bruno@adega.com", UserRole.ESTOQUISTA, name: "Bruno");
        var context = _database.CreateContext();
        var service = new AuthService(context, new PasswordHasher<User>(), new TokenService(Options.Create(AuthServiceTests.Jwt)),
            new TestData.FakeCurrentUser(user.Id), TestData.CreateAuditService(context, user.Id),
            new LoginDtoValidator(), new ChangePasswordDtoValidator());

        await service.ChangePasswordAsync(new ChangePasswordDto(TestData.DefaultPassword, "OutraSenha9"));

        var log = Assert.Single(await LogsAsync(AuditAction.USER_PASSWORD_CHANGED));
        Assert.Equal((user.Id, user.Id, (string?)null, (string?)null), (log.UserId, log.EntityId, log.OldValue, log.NewValue));
    }

    [Fact]
    public async Task GetAll_FiltersByEntityActionAndUser_NewestFirst()
    {
        var product = await TestData.AddProductAsync(_database);
        var other = await TestData.AddUserAsync(_database, "carla@adega.com", UserRole.ADMIN, name: "Carla");
        await using (var context = _database.CreateContext())
        {
            var admin = TestData.CreateAuditService(context);
            admin.Add(AuditAction.PRODUCT_PRICE_CHANGED, "Product", product.Id, product.Name);
            admin.Add(AuditAction.PRODUCT_DEACTIVATED, "Product", product.Id, product.Name);
            TestData.CreateAuditService(context, other.Id).Add(AuditAction.SALE_CANCELED, "Sale", 7, "Venda #7");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var service = TestData.CreateAuditService(_database.CreateContext());
        var forProduct = await service.GetAllAsync(new AuditLogQuery { Entity = "Product", EntityId = product.Id });
        var byCarla = await service.GetAllAsync(new AuditLogQuery { UserId = other.Id });
        var cancels = await service.GetAllAsync(new AuditLogQuery { Action = AuditAction.SALE_CANCELED, From = Today, To = Today });

        Assert.Equal([AuditAction.PRODUCT_DEACTIVATED, AuditAction.PRODUCT_PRICE_CHANGED], forProduct.Items.Select(a => a.Action));
        Assert.Equal("Carla", Assert.Single(byCarla.Items).UserName);
        Assert.Equal("Venda #7", Assert.Single(cancels.Items).Description);
    }

    public void Dispose() => _database.Dispose();
}
