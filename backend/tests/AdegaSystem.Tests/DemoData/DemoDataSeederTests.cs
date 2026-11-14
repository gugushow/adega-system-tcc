using System.Net;
using System.Net.Http.Json;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.Data.DemoData;
using AdegaSystem.Api.DTOs.Auth;
using AdegaSystem.Api.Entities;
using AdegaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static AdegaSystem.Tests.DemoData.DemoDatabaseTests;

namespace AdegaSystem.Tests.DemoData;

/// <summary>Uma API em memória (SQLite) com a demo carregada uma vez, compartilhada pelos testes de leitura.</summary>
public sealed class SeededDemo : IAsyncLifetime
{
    public const string Password = "DemoTeste123";

    public ApiFactory Factory { get; } = new();
    public DemoSeedResult Result { get; private set; }

    public static IConfiguration Configuration(string? password = Password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DemoDataSeeder.PasswordKey] = password })
            .Build();

    public static DemoDataSeeder Seeder(ApiFactory factory, string environment = "Development", string? password = Password) =>
        new(factory.Services, new TestEnvironment(environment), Configuration(password));

    public async ValueTask InitializeAsync() => Result = await Seeder(Factory).RunAsync();

    public ValueTask DisposeAsync()
    {
        Factory.Dispose();
        return ValueTask.CompletedTask;
    }
}

public class DemoDataSeederTests(SeededDemo demo) : IClassFixture<SeededDemo>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Today => BusinessClock.Today;

    private async Task<T> QueryAsync<T>(Func<AdegaDbContext, Task<T>> query)
    {
        using var scope = demo.Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AdegaDbContext>());
    }

    private Task<Product> ProductAsync(string key)
    {
        var name = DemoCatalog.Products.Single(p => p.Key == key).Name;
        return QueryAsync(c => c.Products.AsNoTracking().SingleAsync(p => p.Name == name, Ct));
    }

    // ---------------------------------------------------------------- carga e repetição

    [Fact]
    public async Task Seed_CreatesEveryRecordOfTheCatalogAndTimeline()
    {
        Assert.Equal(DemoSeedResult.Seeded, demo.Result);

        Assert.Equal(12, await QueryAsync(c => c.Categories.CountAsync(Ct)));
        Assert.Equal(DemoCatalog.Products.Length, await QueryAsync(c => c.Products.CountAsync(Ct)));
        Assert.Equal(DemoCatalog.Suppliers.Length, await QueryAsync(c => c.Suppliers.CountAsync(Ct)));
        Assert.Equal(DemoCatalog.Customers.Length, await QueryAsync(c => c.Customers.CountAsync(Ct)));
        Assert.Equal(DemoCatalog.Users.Length + 1, await QueryAsync(c => c.Users.CountAsync(Ct)));
        Assert.Equal(DemoTimeline.Purchases.Length, await QueryAsync(c => c.Purchases.CountAsync(Ct)));
        Assert.Equal(DemoTimeline.Sales.Length, await QueryAsync(c => c.Sales.CountAsync(Ct)));
        Assert.Equal(DemoCatalog.Combos.Length, await QueryAsync(c => c.Combos.CountAsync(Ct)));
        Assert.True(await QueryAsync(c => c.SystemSettings.AnyAsync(s => s.Key == DemoDataSeeder.MarkerKey, Ct)));
    }

    [Fact]
    public async Task Seed_RunAgain_ChangesNothing()
    {
        var before = await CountsAsync();

        var result = await SeededDemo.Seeder(demo.Factory).RunAsync();

        Assert.Equal(DemoSeedResult.AlreadySeeded, result);
        Assert.Equal(before, await CountsAsync());
    }

    private Task<int[]> CountsAsync() => QueryAsync(async c => new[]
    {
        await c.Products.CountAsync(Ct), await c.Suppliers.CountAsync(Ct), await c.Customers.CountAsync(Ct),
        await c.Users.CountAsync(Ct), await c.Purchases.CountAsync(Ct), await c.Sales.CountAsync(Ct),
        await c.StockMovements.CountAsync(Ct), await c.Combos.CountAsync(Ct), await c.AuditLogs.CountAsync(Ct),
    });

    // ---------------------------------------------------------------- integridade

    [Fact]
    public async Task Seed_LeavesStockMovementsAndDatesConsistent()
    {
        // Estoque = lotes = movimentações; lotes com a data da compra; vendas sem lote futuro ou vencido;
        // movimentações e pontos com a data da operação de origem.
        Assert.Empty(await QueryAsync(DemoChecks.FindProblemsAsync));
    }

    [Fact]
    public async Task Purchases_CreateOneBatchMovementAndCostHistoryPerItem()
    {
        var items = DemoTimeline.Purchases.Sum(p => p.Items.Length);

        Assert.Equal(items, await QueryAsync(c => c.ProductBatches.CountAsync(b => b.PurchaseId != null, Ct)));
        Assert.Equal(items, await QueryAsync(c => c.StockMovements.CountAsync(m => m.MovementType == MovementType.PURCHASE, Ct)));
        Assert.Equal(items, await QueryAsync(c => c.ProductPriceHistories.CountAsync(Ct)));
        Assert.Equal(items, await QueryAsync(c => c.PurchaseItems.CountAsync(i => i.BatchId != null, Ct)));
    }

    [Fact]
    public async Task Purchases_ProductCostIsTheMostRecentPurchase()
    {
        // Refrigerante Cola 2L: comprado a 6,20, depois 6,50 e por último 6,35.
        var cola = await ProductAsync("cola-2l");
        var history = await QueryAsync(c => c.ProductPriceHistories.Where(h => h.ProductId == cola.Id)
            .OrderBy(h => h.Date).Select(h => h.CostPrice).ToListAsync(Ct));

        Assert.Equal([6.20m, 6.50m, 6.35m], history);
        Assert.Equal(6.35m, cola.CostPrice);
        Assert.Equal(3, await QueryAsync(c => c.ProductBatches.CountAsync(b => b.ProductId == cola.Id, Ct)));
    }

    [Fact]
    public async Task Purchases_HappenOnDifferentDaysAndBeforeTheSalesThatUseThem()
    {
        var dates = await QueryAsync(c => c.Purchases.Select(p => p.PurchaseDate).Distinct().CountAsync(Ct));
        Assert.Equal(DemoTimeline.Purchases.Select(p => p.Day).Distinct().Count(), dates);

        var consumed = await QueryAsync(c => c.SaleItemBatches
            .Select(b => new { BatchDate = b.Batch.CreatedAt, b.SaleItem.Sale.SaleDate })
            .ToListAsync(Ct));
        Assert.All(consumed, x => Assert.True(x.BatchDate < x.SaleDate));
    }

    // ---------------------------------------------------------------- vendas, FEFO e datas

    [Fact]
    public async Task Sales_FollowFefo()
    {
        // Para cada lote que uma venda usou: nenhum outro lote do mesmo produto, que já existia, não estava
        // vencido e vencia antes, ainda tinha unidades sobrando depois daquela venda.
        var batches = await QueryAsync(c => c.ProductBatches.AsNoTracking().ToListAsync(Ct));
        var movements = await QueryAsync(c => c.StockMovements.AsNoTracking().ToListAsync(Ct));
        var used = await QueryAsync(c => c.SaleItemBatches
            .Select(b => new { b.BatchId, b.Quantity, b.SaleItem.SaleId, b.SaleItem.Sale.SaleDate })
            .ToListAsync(Ct));

        foreach (var item in used)
        {
            var batch = batches.Single(b => b.Id == item.BatchId);
            var earlier = batches.Where(b =>
                b.ProductId == batch.ProductId && b.Id != batch.Id && b.CreatedAt < item.SaleDate
                && (b.ExpirationDate == null || b.ExpirationDate >= Today)
                && Expires(b) < Expires(batch));

            foreach (var other in earlier)
            {
                // Saldo do outro lote logo depois desta venda (movimentações até a data dela).
                var balanceAfter = movements.Where(m => m.BatchId == other.Id && m.CreatedAt <= item.SaleDate).Sum(m => m.Quantity);
                Assert.True(balanceAfter == 0, $"Venda #{item.SaleId} usou o lote {batch.BatchNumber} com {other.BatchNumber} vencendo antes e com saldo.");
            }
        }

        static DateOnly Expires(ProductBatch b) => b.ExpirationDate ?? DateOnly.MaxValue;
    }

    [Fact]
    public async Task Sales_NeverUseExpiredBatches()
    {
        var expired = await QueryAsync(c => c.SaleItemBatches.CountAsync(b => b.Batch.ExpirationDate < Today, Ct));
        Assert.Equal(0, expired);
    }

    [Fact]
    public async Task Sales_AreSpreadOverTheLast30Days()
    {
        var dates = await QueryAsync(c => c.Sales.Select(s => s.SaleDate).ToListAsync(Ct));
        var days = dates.Select(BusinessClock.ToBusinessDate).ToList();

        Assert.All(days, d => Assert.InRange(d, Today.AddDays(-30), Today));
        Assert.True(days.Distinct().Count() >= 25);
        Assert.Equal(4, days.Count(d => d == Today));
        Assert.All(dates, d => Assert.True(d < DateTime.UtcNow));
    }

    [Fact]
    public async Task Sales_CalculateProfitAndLoyalty()
    {
        var sales = await QueryAsync(c => c.Sales.AsNoTracking().ToListAsync(Ct));
        Assert.All(sales, s => Assert.Equal(s.Total - s.TotalCost, s.Profit));
        Assert.All(sales, s => Assert.True(s.TotalCost > 0));

        var earned = await QueryAsync(c => c.LoyaltyTransactions.CountAsync(t => t.Type == LoyaltyType.EARN, Ct));
        Assert.Equal(DemoTimeline.Sales.Count(s => s.Customer is not null), earned);
        Assert.Equal(1, await QueryAsync(c => c.LoyaltyTransactions.CountAsync(t => t.Type == LoyaltyType.REDEEM, Ct)));
    }

    [Fact]
    public async Task CanceledSale_KeepsItsOwnDatesForReturnsPointsAndAudit()
    {
        var sale = await QueryAsync(c => c.Sales.AsNoTracking().SingleAsync(s => s.Status == SaleStatus.CANCELED, Ct));
        Assert.Equal(TimeSpan.FromMinutes(45), sale.CanceledAt - sale.SaleDate);

        var returns = await QueryAsync(c => c.StockMovements
            .Where(m => m.ReferenceType == nameof(Sale) && m.ReferenceId == sale.Id && m.MovementType == MovementType.RETURN)
            .Select(m => m.CreatedAt).ToListAsync(Ct));
        Assert.NotEmpty(returns);
        Assert.All(returns, d => Assert.Equal(sale.CanceledAt, d));

        var audit = await QueryAsync(c => c.AuditLogs.SingleAsync(a => a.Action == AuditAction.SALE_CANCELED && a.EntityId == sale.Id, Ct));
        Assert.Equal(sale.CanceledAt, audit.CreatedAt);
    }

    [Fact]
    public async Task NoRecordKeepsTheTimeOfTheLoad()
    {
        // Nenhum carimbo ficou com a hora em que a carga rodou: todos foram para a data do roteiro.
        var marker = await QueryAsync(c => c.SystemSettings.SingleAsync(s => s.Key == DemoDataSeeder.MarkerKey, Ct));
        var startedAt = DateTime.Parse(marker.Value, null, System.Globalization.DateTimeStyles.RoundtripKind).AddSeconds(-5);

        Assert.False(await QueryAsync(c => c.StockMovements.AnyAsync(x => x.CreatedAt >= startedAt, Ct)));
        Assert.False(await QueryAsync(c => c.Sales.AnyAsync(x => x.SaleDate >= startedAt, Ct)));
        Assert.False(await QueryAsync(c => c.ProductBatches.AnyAsync(x => x.CreatedAt >= startedAt, Ct)));
        Assert.False(await QueryAsync(c => c.LoyaltyTransactions.AnyAsync(x => x.CreatedAt >= startedAt, Ct)));
        Assert.False(await QueryAsync(c => c.AuditLogs.AnyAsync(x => x.CreatedAt >= startedAt, Ct)));
        Assert.False(await QueryAsync(c => c.Products.AnyAsync(x => x.UpdatedAt >= startedAt, Ct)));
    }

    // ---------------------------------------------------------------- cadastros

    [Fact]
    public async Task Users_HaveTheRightRolesAndCanLogIn()
    {
        var client = demo.Factory.CreateClient();
        foreach (var user in DemoCatalog.Users)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto(user.Email, SeededDemo.Password), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var role = await QueryAsync(c => c.Users.Where(u => u.Email == user.Email).Select(u => u.Role).SingleAsync(Ct));
            Assert.Equal(user.Role, role);
        }
    }

    [Fact]
    public void Catalog_BarcodesAreUniqueValidAndInternalUse()
    {
        var codes = DemoCatalog.Products.Select(p => p.Barcode).OfType<string>().ToList();

        Assert.True(codes.Count >= 35);
        Assert.Contains(DemoCatalog.Products, p => p.Barcode is null);
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code =>
        {
            Assert.Equal(13, code.Length);
            Assert.True(Gtin.HasValidCheckDigit(code));
            // Prefixo 2xx: faixa GS1 de uso interno, nunca um produto real de mercado.
            Assert.StartsWith("2", code);
        });
    }

    [Fact]
    public void Catalog_CpfsCnpjsAndEmailsAreValidUniqueAndFictitious()
    {
        var cpfs = DemoCatalog.Customers.Select(c => c.Cpf).OfType<string>().ToList();
        Assert.All(cpfs, cpf => Assert.True(Cpf.IsValid(cpf)));
        Assert.Equal(cpfs.Count, cpfs.Distinct().Count());

        var cnpjs = DemoCatalog.Suppliers.Select(s => s.Cnpj).ToList();
        Assert.All(cnpjs, cnpj => Assert.True(Cnpj.IsValid(cnpj)));
        Assert.Equal(cnpjs.Count, cnpjs.Distinct().Count());

        var emails = DemoCatalog.Users.Select(u => u.Email)
            .Concat(DemoCatalog.Suppliers.Select(s => s.Email))
            .Concat(DemoCatalog.Customers.Select(c => c.Email).OfType<string>());
        Assert.All(emails, email => Assert.EndsWith("@adegasystem.local", email));
    }

    [Fact]
    public async Task Combos_PointToActiveProductsAndCostLessThanTheProducts()
    {
        var combos = await QueryAsync(c => c.Combos.AsNoTracking().Include(x => x.Items).ThenInclude(i => i.Product).ToListAsync(Ct));

        Assert.All(combos, combo =>
        {
            Assert.NotEmpty(combo.Items);
            Assert.All(combo.Items, i => Assert.True(i.Product.Active));
            Assert.True(combo.SalePrice <= combo.Items.Sum(i => i.Quantity * i.Product.SalePrice));
        });
        Assert.Equal(1, combos.Count(c => !c.Active));
    }

    // ---------------------------------------------------------------- cenários para navegar

    [Theory]
    [InlineData("energetico-2l")]
    [InlineData("vodka-premium")]
    [InlineData("suco-pessego")]
    public async Task Scenario_OutOfStock(string key) => Assert.Equal(0, (await ProductAsync(key)).CurrentStock);

    [Theory]
    [InlineData("whisky-12")]
    [InlineData("gin-pink")]
    public async Task Scenario_ExactlyAtMinimum(string key)
    {
        var product = await ProductAsync(key);
        Assert.Equal(product.MinimumStock, product.CurrentStock);
    }

    [Theory]
    [InlineData("ipa")]
    [InlineData("limao-2l")]
    [InlineData("vinho-rose")]
    [InlineData("gin-london")]
    public async Task Scenario_BelowMinimum(string key)
    {
        var product = await ProductAsync(key);
        Assert.InRange(product.CurrentStock, 1, product.MinimumStock - 1);
    }

    [Fact]
    public async Task Scenario_HighStock()
    {
        var product = await ProductAsync("pilsen-lata");
        Assert.True(product.CurrentStock > product.MinimumStock * 5);
    }

    [Fact]
    public async Task Scenario_BatchesExpiredAndAboutToExpire()
    {
        var batches = await QueryAsync(c => c.ProductBatches.AsNoTracking()
            .Where(b => b.QuantityAvailable > 0 && b.ExpirationDate != null).ToListAsync(Ct));

        Assert.Equal(3, batches.Count(b => b.ExpirationDate < Today));
        Assert.True(batches.Count(b => b.ExpirationDate >= Today && b.ExpirationDate <= Today.AddDays(7)) >= 3);
        Assert.True(batches.Count(b => b.ExpirationDate > Today.AddDays(7) && b.ExpirationDate <= Today.AddDays(30)) >= 3);
        Assert.True(batches.Count(b => b.ExpirationDate > Today.AddDays(30) && b.ExpirationDate <= Today.AddDays(60)) >= 3);

        var productsWithSeveralBatches = await QueryAsync(c => c.ProductBatches.GroupBy(b => b.ProductId).CountAsync(g => g.Count() > 1, Ct));
        Assert.True(productsWithSeveralBatches >= 10);
    }

    [Fact]
    public async Task Scenario_LossesWriteOffAndInactiveRecordsWithHistory()
    {
        Assert.Equal(DemoTimeline.Losses.Length, await QueryAsync(c => c.StockMovements.CountAsync(m => m.MovementType == MovementType.LOSS, Ct)));
        Assert.Equal(1, await QueryAsync(c => c.StockMovements.CountAsync(m => m.MovementType == MovementType.EXPIRED, Ct)));

        var inactiveProducts = await QueryAsync(c => c.Products.Where(p => !p.Active).Select(p => p.Id).ToListAsync(Ct));
        Assert.Equal(DemoCatalog.ProductsToDeactivate.Length, inactiveProducts.Count);
        var soldInactive = await QueryAsync(c => c.SaleItems.Where(i => inactiveProducts.Contains(i.ProductId))
            .Select(i => i.ProductId).Distinct().CountAsync(Ct));
        Assert.Equal(inactiveProducts.Count, soldInactive);

        var inactiveSupplier = await QueryAsync(c => c.Suppliers.Include(s => s.Purchases).SingleAsync(s => !s.Active, Ct));
        Assert.NotEmpty(inactiveSupplier.Purchases);
        Assert.True(await QueryAsync(c => c.Suppliers.AnyAsync(s => s.Active && !s.Purchases.Any(), Ct)));

        var inactiveCustomer = await QueryAsync(c => c.Customers.Include(x => x.Sales).SingleAsync(x => !x.Active, Ct));
        Assert.NotEmpty(inactiveCustomer.Sales);
        Assert.True(await QueryAsync(c => c.Customers.CountAsync(x => !x.Sales.Any(), Ct)) >= 10);
    }
}

/// <summary>As travas do seeder, cada uma num banco novo e vazio.</summary>
public class DemoDataSeederGuardTests
{
    private static async Task<bool> HasProductsAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AdegaDbContext>().Products.AnyAsync();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    public async Task Seed_OutsideDevelopment_RefusesWithoutWriting(string environment)
    {
        using var factory = new ApiFactory();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SeededDemo.Seeder(factory, environment).RunAsync());

        Assert.Contains("Development", ex.Message);
        Assert.False(await HasProductsAsync(factory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fraca")]
    public async Task Seed_WithoutAStrongPassword_RefusesWithoutWriting(string? password)
    {
        using var factory = new ApiFactory();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SeededDemo.Seeder(factory, password: password).RunAsync());

        Assert.Contains(DemoDataSeeder.PasswordKey, ex.Message);
        Assert.False(await HasProductsAsync(factory));
    }

    [Fact]
    public async Task Seed_OnADatabaseWithData_Refuses()
    {
        using var factory = new ApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AdegaDbContext>();
            context.Suppliers.Add(new Supplier { Name = "Fornecedor de antes" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SeededDemo.Seeder(factory).RunAsync());

        Assert.Contains("adega_demo", ex.Message);
        Assert.False(await HasProductsAsync(factory));
    }
}
