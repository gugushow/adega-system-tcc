using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems");

        builder.Property(i => i.UnitPrice).HasPrecision(12, 2);
        builder.Property(i => i.CostPrice).HasPrecision(12, 4);
        builder.Property(i => i.Discount).HasPrecision(12, 2);
        builder.Property(i => i.Total).HasPrecision(12, 2);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: um combo já vendido nunca some (só é desativado).
        builder.HasOne(i => i.Combo)
            .WithMany()
            .HasForeignKey(i => i.ComboId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Batches)
            .WithOne(b => b.SaleItem)
            .HasForeignKey(b => b.SaleItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
