using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class ProductPriceHistoryConfiguration : IEntityTypeConfiguration<ProductPriceHistory>
{
    public void Configure(EntityTypeBuilder<ProductPriceHistory> builder)
    {
        builder.ToTable("ProductPriceHistories");

        builder.Property(h => h.CostPrice).HasPrecision(12, 4);

        builder.HasIndex(h => new { h.ProductId, h.Date });

        builder.HasOne(h => h.Product).WithMany().HasForeignKey(h => h.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.Supplier).WithMany().HasForeignKey(h => h.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.Purchase).WithMany().HasForeignKey(h => h.PurchaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
