using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdegaSystem.Api.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(40);
        builder.Property(a => a.Entity).HasMaxLength(30).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(200).IsRequired();
        // JSON pequeno (poucos campos). "longtext" no MySQL, sem limite prático.
        builder.Property(a => a.OldValue);
        builder.Property(a => a.NewValue);

        // A tela lista por data e filtra por registro ("tudo o que aconteceu com este produto").
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => new { a.Entity, a.EntityId });

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
