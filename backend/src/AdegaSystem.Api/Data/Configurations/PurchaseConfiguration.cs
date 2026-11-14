using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");

        builder.Property(p => p.InvoiceNumber).HasMaxLength(50);
        builder.Property(p => p.TotalAmount).HasPrecision(12, 2);

        // A mesma nota não pode ser lançada duas vezes para o mesmo fornecedor.
        // Notas de fornecedores diferentes podem ter o mesmo número.
        builder.HasIndex(p => new { p.SupplierId, p.InvoiceNumber }).IsUnique();
        builder.HasIndex(p => p.PurchaseDate);

        builder.HasOne(p => p.Supplier)
            .WithMany(s => s.Purchases)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade só entre o documento e seus itens: item não existe sem a compra.
        builder.HasMany(p => p.Items)
            .WithOne(i => i.Purchase)
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
