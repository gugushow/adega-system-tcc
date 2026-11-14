using System.Globalization;
using System.Security.Claims;
using AdegaSystem.Api.Auth;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Data.Seed;
using AdegaSystem.Api.DTOs.Batches;
using AdegaSystem.Api.DTOs.Categories;
using AdegaSystem.Api.DTOs.Combos;
using AdegaSystem.Api.DTOs.Customers;
using AdegaSystem.Api.DTOs.Loyalty;
using AdegaSystem.Api.DTOs.Products;
using AdegaSystem.Api.DTOs.Purchases;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.DTOs.Suppliers;
using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Entities;
using AdegaSystem.Api.Services;
using AdegaSystem.Api.Validators;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Data.DemoData;

public enum DemoSeedResult
{
    Seeded,
    AlreadySeeded,
}

/// <summary>
/// Carrega os dados de demonstração (DemoCatalog + DemoTimeline) usando os services reais:
/// compras pelo PurchaseService, vendas pelo SaleService, perdas pelo StockService e assim por diante.
/// Nada de estoque é gravado direto: lotes, movimentações, custo, FEFO e pontos saem das mesmas
/// regras que o sistema usa no dia a dia.
///
/// O único ajuste exclusivo da demo é o de DATAS. Os services gravam tudo com a hora atual; depois
/// de cada operação, o seeder move para a data do roteiro todos os carimbos de tempo que aquela
/// operação acabou de criar (ver DemoTimestamps). Quantidades, lotes, custos e pontos não mudam.
/// </summary>
public sealed class DemoDataSeeder(IServiceProvider services, IHostEnvironment environment, IConfiguration configuration)
{
    public const string PasswordKey = "DemoData:Password";
    public const string MarkerKey = "DemoDataSeededAt";
    public const string AdminKey = "admin";

    // Ids gerados pelo banco, achados pela Key de cada registro do catálogo.
    private readonly Dictionary<string, int> _suppliers = [];
    private readonly Dictionary<string, int> _products = [];
    private readonly Dictionary<string, int> _customers = [];
    private readonly Dictionary<string, int> _users = new() { [AdminKey] = UserSeed.AdminId };
    private readonly Dictionary<string, int> _combos = [];
    private readonly Dictionary<DemoSale, int> _sales = new(ReferenceEqualityComparer.Instance);

    private DateTime _startedAt;
    private DateTime _limit;
    private int _batchCounter;

    public async Task<DemoSeedResult> RunAsync()
    {
        // 1) Ambiente: só Development. Esta conferência fica aqui dentro, e não só no Program.cs,
        //    para que nenhuma outra chamada consiga carregar a demo em produção.
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"A carga de demonstração só roda no ambiente Development (atual: {environment.EnvironmentName}).");
        }

        // 2) Senha dos usuários demo: só da configuração (User Secrets), nunca do código.
        var password = configuration[PasswordKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"Defina a senha dos usuários demo: dotnet user-secrets set \"{PasswordKey}\" \"SUA_SENHA\" --project backend/src/AdegaSystem.Api");
        }

        if (!PasswordRules.IsStrong(password))
        {
            throw new InvalidOperationException(
                $"A senha em \"{PasswordKey}\" é fraca. Use pelo menos {PasswordRules.MinimumLength} caracteres, com letras e números.");
        }

        // 3) O banco precisa estar como a migration deixou: sem a marca da demo e sem dados de operação.
        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AdegaDbContext>();
            if (await context.SystemSettings.AnyAsync(s => s.Key == MarkerKey))
            {
                return DemoSeedResult.AlreadySeeded;
            }

            var hasData = await context.Products.AnyAsync()
                || await context.Suppliers.AnyAsync()
                || await context.Customers.AnyAsync()
                || await context.Purchases.AnyAsync()
                || await context.Sales.AnyAsync()
                || await context.Users.AnyAsync(u => u.Id != UserSeed.AdminId);
            if (hasData)
            {
                // Pode ser uma carga anterior que parou no meio. Completar seria adivinhar o que falta.
                throw new InvalidOperationException(
                    "O banco já tem dados e não tem a marca de carga completa. Recrie o banco adega_demo " +
                    "(veja docs/DADOS-DEMO.md) e rode a carga de novo.");
            }
        }

        // Toda data da demo fica antes deste limite. Tudo o que for gravado depois dele, durante a carga,
        // foi gravado "agora" pelos services e é movido para a data do roteiro.
        _startedAt = DateTime.UtcNow;
        _limit = _startedAt.AddSeconds(-5);

        await CreateCatalogAsync(password);
        await RunTimelineAsync();
        await DeactivateAsync();

        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AdegaDbContext>();
            await DemoChecks.EnsureConsistentAsync(context);

            context.SystemSettings.Add(new SystemSetting
            {
                Key = MarkerKey,
                Value = _startedAt.ToString("O", CultureInfo.InvariantCulture),
            });
            await context.SaveChangesAsync();
        }

        return DemoSeedResult.Seeded;
    }

    // ---------------------------------------------------------------- cadastros

    private async Task CreateCatalogAsync(string password)
    {
        await AtAsync(DemoTimeline.CatalogDay, "08:00", AdminKey, async sp =>
        {
            var categoryService = sp.GetRequiredService<ICategoryService>();
            foreach (var (name, description) in DemoCatalog.NewCategories)
            {
                await categoryService.CreateAsync(new CreateCategoryDto(name, description));
            }

            var userService = sp.GetRequiredService<IUserService>();
            foreach (var user in DemoCatalog.Users)
            {
                var created = await userService.CreateAsync(new CreateUserDto(user.Name, user.Email, password, user.Role));
                _users[user.Key] = created.Id;
            }

            var supplierService = sp.GetRequiredService<ISupplierService>();
            foreach (var s in DemoCatalog.Suppliers)
            {
                var created = await supplierService.CreateAsync(
                    new CreateSupplierDto(s.Name, s.CorporateName, s.Cnpj, s.Phone, s.Email, s.Address));
                _suppliers[s.Key] = created.Id;
            }

            // Custo inicial 0: o custo de verdade vem da primeira compra (PurchaseService).
            var categories = await sp.GetRequiredService<AdegaDbContext>().Categories
                .ToDictionaryAsync(c => c.Name, c => c.Id);
            var productService = sp.GetRequiredService<IProductService>();
            foreach (var p in DemoCatalog.Products)
            {
                var created = await productService.CreateAsync(new CreateProductDto(
                    p.Name, null, p.Barcode, p.Brand, categories[p.Category], p.Volume, p.AlcoholContent, 0, p.SalePrice, p.MinimumStock));
                _products[p.Key] = created.Id;
            }
        });

        await AtAsync(DemoTimeline.CustomersDay, "10:00", "vendedor1", async sp =>
        {
            var customerService = sp.GetRequiredService<ICustomerService>();
            foreach (var c in DemoCatalog.Customers)
            {
                var created = await customerService.CreateAsync(new CreateCustomerDto(c.Name, c.Cpf, c.Phone, c.Email, c.BirthDate));
                _customers[c.Key] = created.Id;
            }
        });
    }

    // ---------------------------------------------------------------- linha do tempo

    /// <summary>Um acontecimento do roteiro: quando, quem fez e o que fazer.</summary>
    private sealed record TimelineEvent(int Day, string Time, string User, Func<IServiceProvider, Task> Run);

    private async Task RunTimelineAsync()
    {
        var events = new List<TimelineEvent>();

        foreach (var purchase in DemoTimeline.Purchases)
        {
            events.Add(new(purchase.Day, "09:00", "estoquista1", sp => CreatePurchaseAsync(sp, purchase)));
        }

        events.Add(new(DemoTimeline.CombosDay, "10:00", AdminKey, CreateCombosAsync));

        foreach (var sale in DemoTimeline.Sales)
        {
            events.Add(new(sale.Day, sale.Time, sale.Seller, sp => CreateSaleAsync(sp, sale)));

            // O cancelamento é outro acontecimento, feito pelo ADMIN 45 minutos depois, com a própria data.
            if (sale.CancelReason is { } reason)
            {
                events.Add(new(sale.Day, AddMinutes(sale.Time, 45), AdminKey, sp => sp.GetRequiredService<ISaleService>()
                    .CancelAsync(_sales[sale], new CancelSaleDto(reason))));
            }
        }

        foreach (var loss in DemoTimeline.Losses)
        {
            events.Add(new(loss.Day, loss.Time, "estoquista2", sp => RegisterLossAsync(sp, loss)));
        }

        var writeOff = DemoTimeline.WriteOff;
        events.Add(new(writeOff.Day, writeOff.Time, "estoquista1", sp => WriteOffExpiredAsync(sp, writeOff.Product)));

        var redemption = DemoTimeline.Redemption;
        events.Add(new(redemption.Day, redemption.Time, "vendedor2", sp => sp.GetRequiredService<ILoyaltyService>()
            .RedeemAsync(_customers[redemption.Customer], new RedeemPointsDto(redemption.Points, redemption.Description))));

        // Ordem cronológica: cada venda só enxerga os lotes das compras que aconteceram antes dela.
        // OrderBy é estável, então eventos no mesmo horário mantêm a ordem em que foram listados.
        foreach (var e in events.OrderBy(e => When(e.Day, e.Time)))
        {
            await AtAsync(e.Day, e.Time, e.User, e.Run);
        }
    }

    private async Task CreatePurchaseAsync(IServiceProvider sp, DemoPurchase purchase)
    {
        var purchaseDate = BusinessClock.Today.AddDays(purchase.Day);
        var items = purchase.Items.Select(i =>
        {
            DateOnly? expiration = i.ExpiresInDays is { } days ? BusinessClock.Today.AddDays(days) : null;
            // Fabricação um pouco antes da compra (só para produtos com validade).
            DateOnly? manufacturing = expiration is null ? null : purchaseDate.AddDays(-10);
            return new CreatePurchaseItemDto(
                _products[i.Product], i.Quantity, i.UnitCost, $"L-DEMO-{++_batchCounter:000}", manufacturing, expiration);
        }).ToList();

        await sp.GetRequiredService<IPurchaseService>().CreateAsync(
            new CreatePurchaseDto(_suppliers[purchase.Supplier], purchase.Invoice, purchaseDate, items));
    }

    private async Task CreateCombosAsync(IServiceProvider sp)
    {
        var comboService = sp.GetRequiredService<IComboService>();
        foreach (var combo in DemoCatalog.Combos)
        {
            var created = await comboService.CreateAsync(new CreateComboDto(
                combo.Name, combo.Description, combo.SalePrice,
                combo.Items.Select(i => new ComboItemInputDto(_products[i.Product], i.Quantity)).ToList()));
            _combos[combo.Key] = created.Id;
        }
    }

    private async Task CreateSaleAsync(IServiceProvider sp, DemoSale sale)
    {
        var saleService = sp.GetRequiredService<ISaleService>();
        var created = await saleService.CreateAsync(new CreateSaleDto(
            sale.Customer is null ? null : _customers[sale.Customer],
            sale.Payment,
            sale.Discount,
            sale.Items.Select(i => new CreateSaleItemDto(_products[i.Product], i.Quantity)).ToList(),
            sale.Combos?.Select(c => new CreateSaleComboDto(_combos[c.Combo], c.Quantity)).ToList()));

        _sales[sale] = created.Id;
    }

    private async Task RegisterLossAsync(IServiceProvider sp, DemoLoss loss)
    {
        // A perda sai do lote que venceria primeiro, como faria quem está no estoque.
        var batchId = await FirstBatchWithBalanceAsync(sp, loss.Product);
        await sp.GetRequiredService<IStockService>().AdjustAsync(
            new StockAdjustmentDto(batchId, MovementType.LOSS, -loss.Quantity, loss.Reason));
    }

    private async Task WriteOffExpiredAsync(IServiceProvider sp, string product)
    {
        var productId = _products[product];
        var today = BusinessClock.Today;
        var batchId = await sp.GetRequiredService<AdegaDbContext>().ProductBatches
            .Where(b => b.ProductId == productId && b.ExpirationDate < today && b.QuantityAvailable > 0)
            .Select(b => b.Id)
            .FirstAsync();
        await sp.GetRequiredService<IBatchService>().WriteOffAsync(batchId, new WriteOffBatchDto(null));
    }

    private async Task<int> FirstBatchWithBalanceAsync(IServiceProvider sp, string product)
    {
        var productId = _products[product];
        var today = BusinessClock.Today;
        return await sp.GetRequiredService<AdegaDbContext>().ProductBatches
            .Where(b => b.ProductId == productId && b.QuantityAvailable > 0 && (b.ExpirationDate == null || b.ExpirationDate >= today))
            .OrderBy(b => b.ExpirationDate == null)
            .ThenBy(b => b.ExpirationDate)
            .ThenBy(b => b.Id)
            .Select(b => b.Id)
            .FirstAsync();
    }

    // ---------------------------------------------------------------- inativos

    private async Task DeactivateAsync()
    {
        await AtAsync(DemoTimeline.DeactivationsDay, "08:30", AdminKey, async sp =>
        {
            var productService = sp.GetRequiredService<IProductService>();
            foreach (var product in DemoCatalog.ProductsToDeactivate)
            {
                await productService.DeactivateAsync(_products[product]);
            }

            await sp.GetRequiredService<ISupplierService>().DeactivateAsync(_suppliers[DemoCatalog.SupplierToDeactivate]);
            await sp.GetRequiredService<ICustomerService>().DeactivateAsync(_customers[DemoCatalog.CustomerToDeactivate]);
            await sp.GetRequiredService<IComboService>().DeactivateAsync(_combos[DemoCatalog.ComboToDeactivate]);
        });
    }

    // ---------------------------------------------------------------- datas

    /// <summary>
    /// Executa uma operação como se fosse feita por "user" no dia/hora do roteiro:
    /// escopo novo (DbContext limpo), usuário logado simulado e, no fim, as datas movidas.
    /// </summary>
    private async Task AtAsync(int day, string time, string user, Func<IServiceProvider, Task> action)
    {
        var when = When(day, time);
        if (when >= _limit)
        {
            throw new InvalidOperationException($"Data da demo no futuro: {when:O}.");
        }

        using var scope = services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        // O CurrentUserService lê o usuário do HttpContext (claim "sub"), como numa requisição de verdade.
        // Fora de uma requisição, criamos um HttpContext só com esse claim, válido apenas neste escopo.
        var previous = accessor.HttpContext;
        accessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AppClaims.UserId, _users[user].ToString())], "DemoData")),
        };

        try
        {
            await action(scope.ServiceProvider);
            await DemoTimestamps.MoveAsync(scope.ServiceProvider.GetRequiredService<AdegaDbContext>(), _limit, when);
        }
        finally
        {
            accessor.HttpContext = previous;
        }
    }

    /// <summary>
    /// Dia e hora do roteiro em UTC (a hora é de Brasília). Hoje (dia 0) é "comprimido" entre a
    /// meia-noite e o momento da carga, para nenhuma venda de hoje ficar no futuro.
    /// </summary>
    private DateTime When(int day, string time)
    {
        var date = BusinessClock.Today.AddDays(day);
        var start = BusinessClock.StartOfDayUtc(date);
        var timeOfDay = TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan();

        if (day < 0)
        {
            return start + timeOfDay;
        }

        var elapsedToday = _limit - start;
        return start + elapsedToday * (timeOfDay / TimeSpan.FromDays(1));
    }

    private static string AddMinutes(string time, int minutes) =>
        TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture).AddMinutes(minutes).ToString("HH:mm", CultureInfo.InvariantCulture);
}
