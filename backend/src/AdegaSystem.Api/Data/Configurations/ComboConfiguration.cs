using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class ComboConfiguration : IEntityTypeConfiguration<Combo>
{
    public void Configure(EntityTypeBuilder<Combo> builder)
    {
        builder.ToTable("Combos");

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.SalePrice).HasPrecision(12, 2);

        builder.HasIndex(c => c.Name);

        // Os itens são parte do combo: não existem sem ele (o combo nunca é apagado, só desativado).
        builder.HasMany(c => c.Items)
            .WithOne(i => i.Combo)
            .HasForeignKey(i => i.ComboId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
