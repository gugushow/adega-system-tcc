using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Purchases;

namespace AdegaSystem.Api.Services;

public interface IPurchaseService
{
    Task<PagedResult<PurchaseDto>> GetAllAsync(PurchaseQuery query);
    Task<PurchaseDetailsDto> GetByIdAsync(int id);

    /// <summary>Grava a compra e, na mesma transação, cria os lotes, dá entrada no estoque e atualiza o custo.</summary>
    Task<PurchaseDetailsDto> CreateAsync(CreatePurchaseDto dto);
}
