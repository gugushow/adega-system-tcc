namespace AdegaSystem.Api.Entities;

/// <summary>Tipo de uma linha do extrato de pontos.</summary>
public enum LoyaltyType
{
    /// <summary>Pontos ganhos numa venda (+).</summary>
    EARN,

    /// <summary>Pontos trocados pelo cliente (−).</summary>
    REDEEM,

    /// <summary>Correção manual pelo ADMIN ou estorno de venda cancelada (+ ou −).</summary>
    ADJUSTMENT,
}
