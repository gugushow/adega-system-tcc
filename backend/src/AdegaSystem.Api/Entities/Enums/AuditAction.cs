namespace AdegaSystem.Api.Entities;

/// <summary>Ações registradas na auditoria. Gravadas como texto no banco.</summary>
public enum AuditAction
{
    /// <summary>Ajuste manual ou perda de estoque (tela de Estoque).</summary>
    STOCK_ADJUSTED,

    /// <summary>Baixa de lote vencido (tela de Lotes).</summary>
    BATCH_WRITTEN_OFF,

    /// <summary>Cancelamento de venda.</summary>
    SALE_CANCELED,

    /// <summary>Mudança no preço de custo ou de venda pelo cadastro do produto.</summary>
    PRODUCT_PRICE_CHANGED,

    PRODUCT_DEACTIVATED,
    PRODUCT_REACTIVATED,

    USER_CREATED,

    /// <summary>Nome, e-mail, perfil, situação ou senha redefinida pelo ADMIN.</summary>
    USER_UPDATED,

    USER_DEACTIVATED,

    /// <summary>O próprio usuário trocou a senha.</summary>
    USER_PASSWORD_CHANGED,

    COMBO_CREATED,
    COMBO_UPDATED,
    COMBO_DEACTIVATED,

    /// <summary>Resgate de pontos de fidelidade.</summary>
    LOYALTY_REDEEMED,

    /// <summary>Ajuste manual de pontos pelo ADMIN.</summary>
    LOYALTY_ADJUSTED,

    /// <summary>Margem mínima ou regras de fidelidade alteradas.</summary>
    SETTINGS_UPDATED,
}
