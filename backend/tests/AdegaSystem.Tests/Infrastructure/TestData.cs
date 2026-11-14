using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using Microsoft.AspNetCore.Identity;

namespace AdegaSystem.Tests.Infrastructure;

/// <summary>
/// Atalhos para criar dados de apoio direto no banco de teste, sem passar pelos services.
/// Assim cada teste foca só na regra que está testando.
/// </summary>
public static class TestData
{
    // Ids criados pelo seed (ver CategorySeed e UserSeed).
    public const int CervejaCategoryId = 1;
    public const int VinhoCategoryId = 2;
    public const int AdminUserId = 1;

    public static async Task<Product> AddProductAsync(
        TestDatabase database,
        string name = "Heineken 350ml",
        decimal costPrice = 3.90m,
        bool active = true,
        decimal salePrice = 6.50m,
        int categoryId = CervejaCategoryId)
    {
        await using var context = database.CreateContext();
        var product = new Product
        {
            Name = name,
            CategoryId = categoryId,
            CostPrice = costPrice,
            SalePrice = salePrice,
            MinimumStock = 10,
            Active = active,
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    public static async Task<Supplier> AddSupplierAsync(TestDatabase database, string name = "Distribuidora Boa Bebida", bool active = true)
    {
        await using var context = database.CreateContext();
        var supplier = new Supplier { Name = name, Active = active };
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier;
    }

    public static async Task<Customer> AddCustomerAsync(TestDatabase database, string name = "Maria Souza", bool active = true)
    {
        await using var context = database.CreateContext();
        var customer = new Customer { Name = name, Active = active };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    /// <summary>
    /// Dá entrada num lote novo pelo StockService (o único caminho para criar estoque),
    /// dentro de uma transação, como faz o PurchaseService.
    /// </summary>
    public static async Task<ProductBatch> ReceiveBatchAsync(
        TestDatabase database,
        int productId,
        int quantity,
        DateOnly? expirationDate = null,
        string batchNumber = "L-TESTE",
        decimal unitCost = 3.90m)
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var batch = await CreateStockService(context).ReceiveBatchAsync(new ReceiveBatchRequest(
            productId, quantity, unitCost, batchNumber, null, expirationDate, null, "Entrada de teste"));
        await transaction.CommitAsync();
        return batch;
    }

    public static AuditService CreateAuditService(AdegaDbContext context, int userId = AdminUserId) =>
        new(context, new FakeCurrentUser(userId));

    public static StockService CreateStockService(AdegaDbContext context) =>
        new(context, new FakeCurrentUser(), CreateAuditService(context), new StockAdjustmentDtoValidator());

    public static SaleService CreateSaleService(AdegaDbContext context) =>
        new(context, new FakeCurrentUser(), CreateStockService(context), CreateLoyaltyService(context), CreateAuditService(context),
            new CreateSaleDtoValidator(), new CancelSaleDtoValidator());

    public static SettingsService CreateSettingsService(AdegaDbContext context) =>
        new(context, CreateAuditService(context), new MarginSettingsDtoValidator(), new LoyaltySettingsDtoValidator());

    public static LoyaltyService CreateLoyaltyService(AdegaDbContext context, int userId = AdminUserId) =>
        new(context, new FakeCurrentUser(userId), CreateSettingsService(context), CreateAuditService(context, userId),
            new RedeemPointsDtoValidator(), new AdjustPointsDtoValidator());

    public static ComboService CreateComboService(AdegaDbContext context) =>
        new(context, CreateAuditService(context), new CreateComboDtoValidator(), new UpdateComboDtoValidator());

    public static ReportService CreateReportService(AdegaDbContext context) =>
        new(context, CreateSettingsService(context), new ReportPeriodQueryValidator(), new SalesReportQueryValidator(),
            new ProductRankingQueryValidator(), new IdleProductsQueryValidator());

    /// <summary>Lê o estoque atual direto do banco, num contexto novo.</summary>
    public static async Task<int> GetStockAsync(TestDatabase database, int productId)
    {
        await using var context = database.CreateContext();
        var product = await context.Products.FindAsync(productId);
        return product!.CurrentStock;
    }

    /// <summary>Saldo de um lote direto do banco.</summary>
    public static async Task<int> GetBatchQuantityAsync(TestDatabase database, int batchId)
    {
        await using var context = database.CreateContext();
        var batch = await context.ProductBatches.FindAsync(batchId);
        return batch!.QuantityAvailable;
    }

    /// <summary>A regra central: CurrentStock é sempre a soma dos saldos dos lotes do produto.</summary>
    public static async Task AssertStockMatchesBatchesAsync(TestDatabase database, int productId)
    {
        await using var context = database.CreateContext();
        var product = await context.Products.FindAsync(productId);
        var batchTotal = context.ProductBatches.Where(b => b.ProductId == productId).AsEnumerable().Sum(b => b.QuantityAvailable);
        Assert.Equal(batchTotal, product!.CurrentStock);
    }

    /// <summary>
    /// Cria um usuário com a senha já em hash, como o UserService faz.
    /// </summary>
    public static async Task<User> AddUserAsync(
        TestDatabase database,
        string email,
        UserRole role,
        string password = DefaultPassword,
        bool active = true,
        string name = "Usuário de Teste")
    {
        await using var context = database.CreateContext();
        var user = new User { Name = name, Email = email, Role = role, Active = active };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public const string DefaultPassword = "Senha123";

    /// <summary>Usuário "logado" nos testes de services. Por padrão, o admin semeado.</summary>
    public sealed class FakeCurrentUser(int userId = AdminUserId) : ICurrentUserService
    {
        public int UserId => userId;
    }
}
