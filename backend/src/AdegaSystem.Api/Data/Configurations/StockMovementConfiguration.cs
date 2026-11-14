using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");

        builder.Property(m => m.MovementType).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Reason).HasMaxLength(255).IsRequired();
        builder.Property(m => m.ReferenceType).HasMaxLength(30);

        // Consultas mais comuns: extrato de um produto e listagem por data.
        builder.HasIndex(m => new { m.ProductId, m.CreatedAt });
        builder.HasIndex(m => m.CreatedAt);
        builder.HasIndex(m => new { m.ReferenceType, m.ReferenceId });

        builder.HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Batch)
            .WithMany()
            .HasForeignKey(m => m.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
