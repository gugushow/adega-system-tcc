using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Sales;

namespace AdegaSystem.Api.Services;

public interface ISaleService
{
    Task<PagedResult<SaleDto>> GetAllAsync(SaleQuery query);
    Task<SaleDetailsDto> GetByIdAsync(int id);

    /// <summary>Registra a venda e dá baixa no estoque pelos lotes que vencem primeiro (FEFO), numa transação.</summary>
    Task<SaleDetailsDto> CreateAsync(CreateSaleDto dto);

    /// <summary>Cancela a venda e devolve cada unidade ao lote de onde saiu, numa transação.</summary>
    Task<SaleDetailsDto> CancelAsync(int id, CancelSaleDto dto);
}
