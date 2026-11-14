using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class SaleItemBatchConfiguration : IEntityTypeConfiguration<SaleItemBatch>
{
    public void Configure(EntityTypeBuilder<SaleItemBatch> builder)
    {
        builder.ToTable("SaleItemBatches");

        builder.Property(b => b.UnitCost).HasPrecision(12, 4);

        // Restrict: um lote que já foi vendido nunca pode ser apagado.
        builder.HasOne(b => b.Batch)
            .WithMany()
            .HasForeignKey(b => b.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
