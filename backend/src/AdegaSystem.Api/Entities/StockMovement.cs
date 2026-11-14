namespace AdegaSystem.Api.Entities;

/// <summary>
/// Uma linha do "extrato" do estoque. Toda alteração de CurrentStock gera exatamente uma
/// movimentação, gravada pelo StockService. Movimentações nunca são editadas nem apagadas:
/// para corrigir um erro, faz-se um novo ajuste.
/// </summary>
public class StockMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>
    /// Lote afetado. Toda movimentação a partir da Fase 3 tem lote; fica nulo só
    /// nas movimentações antigas, gravadas antes de os lotes existirem.
    /// </summary>
    public int? BatchId { get; set; }
    public ProductBatch? Batch { get; set; }

    public MovementType MovementType { get; set; }

    /// <summary>Com sinal: positivo para entrada, negativo para saída.</summary>
    public int Quantity { get; set; }

    /// <summary>Estoque do produto antes e depois desta movimentação.</summary>
    public int PreviousStock { get; set; }
    public int NewStock { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Documento que originou a movimentação, ex.: ("Purchase", 15). Nulo em ajustes manuais.</summary>
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
