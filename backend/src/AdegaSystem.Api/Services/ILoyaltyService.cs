using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Loyalty;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Services;

public interface ILoyaltyService
{
    Task<LoyaltyStatementDto> GetStatementAsync(int customerId, PagedQuery query);
    Task<LoyaltyStatementDto> RedeemAsync(int customerId, RedeemPointsDto dto);
    Task<LoyaltyStatementDto> AdjustAsync(int customerId, AdjustPointsDto dto);

    // Chamados pelo SaleService, dentro da transação da venda. Só adicionam ao contexto:
    // quem salva é o SaleService, junto com a venda.

    /// <summary>Pontos ganhos numa venda concluída com cliente. Devolve quantos pontos foram dados (0 se nenhum).</summary>
    Task<int> AddPointsForSaleAsync(Sale sale);

    /// <summary>Estorna os pontos que uma venda deu, quando ela é cancelada.</summary>
    Task ReversePointsForSaleAsync(Sale sale);
}
