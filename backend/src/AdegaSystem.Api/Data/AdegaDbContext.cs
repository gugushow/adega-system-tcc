using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Data;

public class AdegaDbContext(DbContextOptions<AdegaDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<ProductPriceHistory> ProductPriceHistories => Set<ProductPriceHistory>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleItemBatch> SaleItemBatches => Set<SaleItemBatch>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<ComboItem> ComboItems => Set<ComboItem>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Vale para todas as propriedades DateTime de todas as entidades.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();

        // Datas sem hora (data da compra, validade): coluna date no MySQL.
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        configurationBuilder.Properties<DateOnly?>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Registra todas as classes IEntityTypeConfiguration<T> da pasta Data/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdegaDbContext).Assembly);
    }
}
