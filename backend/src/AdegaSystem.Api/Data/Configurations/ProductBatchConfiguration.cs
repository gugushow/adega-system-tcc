using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class ProductBatchConfiguration : IEntityTypeConfiguration<ProductBatch>
{
    public void Configure(EntityTypeBuilder<ProductBatch> builder)
    {
        builder.ToTable("ProductBatches");

        builder.Property(b => b.BatchNumber).HasMaxLength(50).IsRequired();
        builder.Property(b => b.UnitCost).HasPrecision(12, 4);

        // Mesmo motivo do CurrentStock: duas saídas simultâneas do mesmo lote não podem se sobrescrever.
        builder.Property(b => b.QuantityAvailable).IsConcurrencyToken();

        // Consultas de validade (vencidos, vencendo) e a escolha FEFO dos lotes de um produto na venda.
        builder.HasIndex(b => b.ExpirationDate);
        builder.HasIndex(b => new { b.ProductId, b.ExpirationDate });

        builder.HasOne(b => b.Product)
            .WithMany()
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Purchase)
            .WithMany()
            .HasForeignKey(b => b.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
