using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class ComboItemConfiguration : IEntityTypeConfiguration<ComboItem>
{
    public void Configure(EntityTypeBuilder<ComboItem> builder)
    {
        builder.ToTable("ComboItems");

        // Um produto aparece uma vez só em cada combo (a quantidade diz quantas unidades).
        builder.HasIndex(i => new { i.ComboId, i.ProductId }).IsUnique();

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
