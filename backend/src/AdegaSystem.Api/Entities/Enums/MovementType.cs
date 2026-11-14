namespace AdegaSystem.Api.Entities;

/// <summary>
/// Motivo de uma movimentação de estoque. Cada tipo tem um sinal fixo de quantidade
/// (ver StockService): entradas são positivas e saídas são negativas.
/// </summary>
public enum MovementType
{
    /// <summary>Entrada por compra de fornecedor (+).</summary>
    PURCHASE,

    /// <summary>Saída por venda (−), uma por lote usado (FEFO).</summary>
    SALE,

    /// <summary>Correção manual, como uma contagem de inventário (+ ou −).</summary>
    ADJUSTMENT,

    /// <summary>Perda: quebra, avaria, consumo interno (−).</summary>
    LOSS,

    /// <summary>Baixa de lote vencido (−), pela tela de Lotes.</summary>
    EXPIRED,

    /// <summary>Devolução que volta ao estoque, como o cancelamento de uma venda (+).</summary>
    RETURN,
}
