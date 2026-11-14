using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Cpf).HasMaxLength(11);
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.Property(c => c.Email).HasMaxLength(150);

        // Único, mas vários clientes podem ficar sem CPF (NULL), como o CNPJ dos fornecedores.
        builder.HasIndex(c => c.Cpf).IsUnique();
        builder.HasIndex(c => c.Name);
    }
}
