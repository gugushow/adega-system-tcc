using AdegaSystem.Api.Common;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.DTOs.Loyalty;

/// <summary>Resposta do GET /api/customers/{id}/loyalty: saldo, regra atual e uma página do extrato.</summary>
public record LoyaltyStatementDto(
    int CustomerId,
    string CustomerName,
    int Balance,
    bool ProgramEnabled,
    decimal AmountPerPoint,
    PagedResult<LoyaltyTransactionDto> Transactions);

public record LoyaltyTransactionDto(
    int Id,
    DateTime CreatedAt,
    LoyaltyType Type,
    int Points,
    string Description,
    int? SaleId,
    string UserName);

/// <summary>Corpo do POST /api/customers/{id}/loyalty/redeem. Description diz pelo que os pontos foram trocados.</summary>
public record RedeemPointsDto(int Points, string Description);

/// <summary>Corpo do POST /api/customers/{id}/loyalty/adjust (só ADMIN). Points positivo soma, negativo tira.</summary>
public record AdjustPointsDto(int Points, string Reason);
