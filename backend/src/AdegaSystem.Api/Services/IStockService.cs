using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Services;

public interface IStockService
{
    // Os dois métodos abaixo são os únicos caminhos para mudar o estoque. Precisam ser chamados
    // dentro de uma transação aberta por quem chama (compra, ajuste, baixa de vencidos, venda),
    // para que tudo seja gravado ou nada seja.

    /// <summary>Entrada que cria um lote novo (compras).</summary>
    Task<ProductBatch> ReceiveBatchAsync(ReceiveBatchRequest request);

    /// <summary>Entrada ou saída num lote existente. Atualiza lote, produto e grava a movimentação.</summary>
    Task<StockMovement> RegisterMovementAsync(StockMovementRequest request);

    /// <summary>Ajuste manual ou perda, feito pela tela de estoque.</summary>
    Task<StockMovementDto> AdjustAsync(StockAdjustmentDto dto);

    Task<PagedResult<StockMovementDto>> GetMovementsAsync(StockMovementQuery query);
}
