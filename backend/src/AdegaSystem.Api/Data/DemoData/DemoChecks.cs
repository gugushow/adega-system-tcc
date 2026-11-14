using AdegaSystem.Api.Common;
using AdegaSystem.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Data.DemoData;

/// <summary>
/// Conferência final da carga, antes de gravar a marca de "demo carregada". Se algo não bater,
/// a carga falha com a lista de problemas. Os testes usam a mesma conferência.
/// </summary>
public static class DemoChecks
{
    public static async Task EnsureConsistentAsync(AdegaDbContext context)
    {
        var problems = await FindProblemsAsync(context);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException("A carga de demonstração ficou inconsistente: " + string.Join(" | ", problems.Take(10)));
        }
    }

    public static async Task<List<string>> FindProblemsAsync(AdegaDbContext context)
    {
        var problems = new List<string>();

        var products = await context.Products.AsNoTracking().ToListAsync();
        var batches = await context.ProductBatches.AsNoTracking().ToListAsync();
        var movements = await context.StockMovements.AsNoTracking().ToListAsync();
        var purchases = await context.Purchases.AsNoTracking().ToDictionaryAsync(p => p.Id);
        var sales = await context.Sales.AsNoTracking().ToDictionaryAsync(s => s.Id);
        var saleBatches = await context.SaleItemBatches.AsNoTracking()
            .Select(b => new { b.SaleItem.SaleId, b.BatchId })
            .ToListAsync();
        var loyalty = await context.LoyaltyTransactions.AsNoTracking().Where(t => t.SaleId != null).ToListAsync();
        var batchesById = batches.ToDictionary(b => b.Id);

        // 1) Estoque: produto = soma dos lotes = soma das movimentações.
        foreach (var product in products)
        {
            var inBatches = batches.Where(b => b.ProductId == product.Id).Sum(b => b.QuantityAvailable);
            var inMovements = movements.Where(m => m.ProductId == product.Id).Sum(m => m.Quantity);
            if (product.CurrentStock != inBatches || product.CurrentStock != inMovements)
            {
                problems.Add($"{product.Name}: estoque {product.CurrentStock}, lotes {inBatches}, movimentações {inMovements}");
            }
        }

        // 2) Compras: lote e entrada no estoque com a data da compra, que é o dia da nota.
        foreach (var purchase in purchases.Values)
        {
            if (BusinessClock.ToBusinessDate(purchase.CreatedAt) != purchase.PurchaseDate)
            {
                problems.Add($"Compra #{purchase.Id}: registrada em dia diferente da nota");
            }
        }

        foreach (var batch in batches.Where(b => b.PurchaseId != null))
        {
            if (batch.CreatedAt != purchases[batch.PurchaseId!.Value].CreatedAt)
            {
                problems.Add($"Lote {batch.BatchNumber}: data diferente da compra");
            }
        }

        // 3) Vendas: todo lote consumido já existia e não estava vencido no dia da venda.
        foreach (var item in saleBatches)
        {
            var sale = sales[item.SaleId];
            var batch = batchesById[item.BatchId];
            if (batch.CreatedAt > sale.SaleDate)
            {
                problems.Add($"Venda #{sale.Id}: usou o lote {batch.BatchNumber}, que só entrou depois");
            }

            if (batch.ExpirationDate < BusinessClock.ToBusinessDate(sale.SaleDate))
            {
                problems.Add($"Venda #{sale.Id}: usou o lote vencido {batch.BatchNumber}");
            }
        }

        // 4) Tudo o que uma venda gerou tem a data dela; o que o cancelamento gerou, a do cancelamento.
        foreach (var movement in movements)
        {
            var expected = movement switch
            {
                { ReferenceType: nameof(Purchase), MovementType: MovementType.PURCHASE } => purchases[movement.ReferenceId!.Value].CreatedAt,
                { ReferenceType: nameof(Sale), MovementType: MovementType.SALE } => sales[movement.ReferenceId!.Value].SaleDate,
                { ReferenceType: nameof(Sale), MovementType: MovementType.RETURN } => sales[movement.ReferenceId!.Value].CanceledAt,
                _ => (DateTime?)null,
            };
            if (expected is not null && movement.CreatedAt != expected)
            {
                problems.Add($"Movimentação #{movement.Id} ({movement.MovementType}): data diferente da operação de origem");
            }
        }

        foreach (var transaction in loyalty)
        {
            var sale = sales[transaction.SaleId!.Value];
            var expected = transaction.Type == LoyaltyType.EARN ? sale.SaleDate : sale.CanceledAt;
            if (transaction.CreatedAt != expected)
            {
                problems.Add($"Pontos #{transaction.Id} ({transaction.Type}): data diferente da venda #{sale.Id}");
            }
        }

        // 5) Nada no futuro.
        var now = DateTime.UtcNow;
        if (sales.Values.Any(s => s.SaleDate > now) || movements.Any(m => m.CreatedAt > now))
        {
            problems.Add("Há vendas ou movimentações com data no futuro");
        }

        return problems;
    }
}
