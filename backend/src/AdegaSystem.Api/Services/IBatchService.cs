using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Batches;

namespace AdegaSystem.Api.Services;

public interface IBatchService
{
    Task<PagedResult<BatchDto>> GetAllAsync(BatchQuery query);
    Task<BatchDto> GetByIdAsync(int id);

    /// <summary>Baixa de um lote vencido: retira todo o saldo com uma movimentação EXPIRED.</summary>
    Task<BatchDto> WriteOffAsync(int id, WriteOffBatchDto dto);
}
