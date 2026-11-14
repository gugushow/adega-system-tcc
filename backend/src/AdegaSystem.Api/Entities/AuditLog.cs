namespace AdegaSystem.Api.Entities;

/// <summary>
/// Registro de auditoria: quem fez o quê, em qual registro, e como estava antes e depois.
/// Só é gravado para ações importantes (ver AuditAction), nunca editado nem apagado.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public AuditAction Action { get; set; }

    /// <summary>Tipo do registro afetado: "Product", "Sale", "User", "Combo"...</summary>
    public string Entity { get; set; } = string.Empty;

    public int EntityId { get; set; }

    /// <summary>
    /// Nome legível do registro no momento da ação ("Heineken 350ml", "Venda #12").
    /// Fica gravado para a tela não precisar buscar em várias tabelas, e continua certo mesmo se o nome mudar depois.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Valores antes da ação, em JSON (ex.: {"salePrice":6.50}). Nulo quando não se aplica.</summary>
    public string? OldValue { get; set; }

    /// <summary>Valores depois da ação, em JSON.</summary>
    public string? NewValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
