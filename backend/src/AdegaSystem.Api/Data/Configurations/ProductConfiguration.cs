using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

/// <summary>Mapeamento da tabela Products com Fluent API.</summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.Barcode).HasMaxLength(50);
        builder.Property(p => p.Brand).HasMaxLength(100);
        builder.Property(p => p.Volume).HasMaxLength(30);

        // decimal sem precisão definida vira um tipo genérico. Aqui deixamos explícito:
        // preço de venda com 2 casas; custo com 4, porque o custo unitário de uma caixa
        // (ex.: R$ 46,77 / 12 = R$ 3,8975) precisa de mais precisão.
        builder.Property(p => p.AlcoholContent).HasPrecision(5, 2);
        builder.Property(p => p.CostPrice).HasPrecision(12, 4);
        builder.Property(p => p.SalePrice).HasPrecision(12, 2);

        // Token de concorrência: o UPDATE do estoque só acontece se o valor no banco ainda for
        // o mesmo que foi lido. Se duas operações mexerem no mesmo produto ao mesmo tempo,
        // a segunda falha (DbUpdateConcurrencyException) em vez de sobrescrever a primeira.
        builder.Property(p => p.CurrentStock).IsConcurrencyToken();

        // Propriedades calculadas não viram colunas.
        builder.Ignore(p => p.IsLowStock);
        builder.Ignore(p => p.IsOutOfStock);

        // Índice único: o MySQL permite vários NULL num índice único,
        // então produtos sem código de barras não conflitam entre si.
        builder.HasIndex(p => p.Barcode).IsUnique();
        builder.HasIndex(p => p.Name);

        // Restrict: o banco recusa apagar uma categoria que tenha produtos.
        // O padrão do EF seria Cascade, que apagaria os produtos junto.
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
