using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.CorporateName).HasMaxLength(200);
        builder.Property(s => s.Cnpj).HasMaxLength(14);
        builder.Property(s => s.Phone).HasMaxLength(20);
        builder.Property(s => s.Email).HasMaxLength(150);
        builder.Property(s => s.Address).HasMaxLength(300);

        // Como no código de barras: único, mas vários fornecedores podem ficar sem CNPJ (NULL).
        builder.HasIndex(s => s.Cnpj).IsUnique();
        builder.HasIndex(s => s.Name);
    }
}
