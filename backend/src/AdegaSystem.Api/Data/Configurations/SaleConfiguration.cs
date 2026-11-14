using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");

        builder.Property(s => s.Subtotal).HasPrecision(12, 2);
        builder.Property(s => s.Discount).HasPrecision(12, 2);
        builder.Property(s => s.Total).HasPrecision(12, 2);
        builder.Property(s => s.TotalCost).HasPrecision(12, 2);
        builder.Property(s => s.Profit).HasPrecision(12, 2);

        // Enums como texto, legíveis numa consulta direta no banco.
        builder.Property(s => s.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.CancelReason).HasMaxLength(255);

        // Token de concorrência: o cancelamento só grava se a venda ainda estiver COMPLETED no banco.
        // Dois cancelamentos ao mesmo tempo não conseguem devolver o estoque duas vezes.
        builder.Property(s => s.Status).IsConcurrencyToken();

        // Listagem por período e, na Fase 6, relatórios que ignoram as canceladas.
        builder.HasIndex(s => s.SaleDate);
        builder.HasIndex(s => new { s.Status, s.SaleDate });

        builder.HasOne(s => s.Customer)
            .WithMany(c => c.Sales)
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.CanceledByUser)
            .WithMany()
            .HasForeignKey(s => s.CanceledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade só do documento para os seus itens, como na compra.
        builder.HasMany(s => s.Items)
            .WithOne(i => i.Sale)
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
