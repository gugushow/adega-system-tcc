using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Data.DemoData;

/// <summary>
/// O único ajuste exclusivo da demo: mover para a data do roteiro os carimbos de tempo que uma
/// operação acabou de gravar com a hora atual.
///
/// Como funciona: toda data da demo é anterior ao início da carga ("since"). Então, logo depois de
/// uma operação, qualquer data maior ou igual a "since" foi gravada por ela, e vai para "when".
/// Isso cobre tudo o que a operação gera, sem precisar saber quais Ids ela criou:
/// - venda: Sale.SaleDate, as StockMovement de saída, a LoyaltyTransaction de pontos;
/// - cancelamento: Sale.CanceledAt, as StockMovement de devolução, o estorno de pontos e a AuditLog;
/// - compra: Purchase.CreatedAt, os ProductBatch, as StockMovement de entrada e Product.UpdatedAt (custo);
/// - perdas, baixas, resgates, cadastros e desativações: as próprias datas e a AuditLog de cada um.
///
/// Só colunas de data mudam. Quantidade, lote, custo, lucro, saldo e pontos nunca são tocados.
/// (ProductPriceHistory.Date e Purchase.PurchaseDate já nascem com a data da nota, que o seeder informa.)
/// </summary>
public static class DemoTimestamps
{
    public static async Task MoveAsync(AdegaDbContext context, DateTime since, DateTime when)
    {
        // Numa transação: ou todas as datas da operação mudam juntas, ou nenhuma.
        await using var transaction = await context.Database.BeginTransactionAsync();

        await context.Categories.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));
        await context.Users.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));
        await context.Suppliers.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));
        await context.Customers.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));

        await context.Products.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));
        await context.Products.Where(x => x.UpdatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, when));
        await context.Combos.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));
        await context.Combos.Where(x => x.UpdatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, when));

        await context.Purchases.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));
        await context.ProductBatches.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));
        await context.StockMovements.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));

        await context.Sales.Where(x => x.SaleDate >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.SaleDate, when));
        await context.Sales.Where(x => x.CanceledAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CanceledAt, when));
        await context.LoyaltyTransactions.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));
        await context.AuditLogs.Where(x => x.CreatedAt >= since).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));

        await transaction.CommitAsync();
    }
}
