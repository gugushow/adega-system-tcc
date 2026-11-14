using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.CostHistory;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>Histórico do custo de compra de um produto, gravado a cada compra pelo PurchaseService.</summary>
public class CostHistoryService(AdegaDbContext context) : ICostHistoryService
{
    public async Task<CostHistoryDto> GetByProductAsync(int productId)
    {
        var product = await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException("Produto não encontrado.");

        var rows = await context.ProductPriceHistories.AsNoTracking()
            .Where(h => h.ProductId == productId)
            .OrderBy(h => h.Date)
            .ThenBy(h => h.Id)
            .Select(h => new { h.Id, h.Date, h.CostPrice, h.SupplierId, SupplierName = h.Supplier.Name, h.PurchaseId })
            .ToListAsync();

        // Cada registro é comparado com o anterior (já estão em ordem de data).
        var entries = rows
            .Select((row, index) => new CostHistoryEntryDto(
                row.Id,
                row.Date,
                row.CostPrice,
                index == 0 ? null : CalculateChangePercent(rows[index - 1].CostPrice, row.CostPrice),
                row.SupplierId,
                row.SupplierName,
                row.PurchaseId))
            .ToList();

        var totalChange = rows.Count < 2 ? null : CalculateChangePercent(rows[0].CostPrice, rows[^1].CostPrice);

        return new CostHistoryDto(product.Id, product.Name, product.CostPrice, totalChange, entries);
    }

    /// <summary>Variação (%) = ((novo - antigo) / antigo) * 100, com 2 casas. Nulo se o antigo for zero (brinde).</summary>
    private static decimal? CalculateChangePercent(decimal previous, decimal current) =>
        previous == 0 ? null : Math.Round((current - previous) / previous * 100, 2, MidpointRounding.AwayFromZero);
}
