using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Loyalty;
using AdegaSystem.Api.Entities;
using FluentValidation;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Programa de fidelidade: cada R$ X gastos (configurável) vira 1 ponto.
/// O saldo é sempre a soma do extrato (LoyaltyTransaction), nunca um número guardado à parte,
/// assim é impossível o saldo "desencontrar" do histórico.
/// </summary>
public class LoyaltyService(
    AdegaDbContext context,
    ICurrentUserService currentUser,
    ISettingsService settingsService,
    IAuditService auditService,
    IValidator<RedeemPointsDto> redeemValidator,
    IValidator<AdjustPointsDto> adjustValidator) : ILoyaltyService
{
    public async Task<LoyaltyStatementDto> GetStatementAsync(int customerId, PagedQuery query)
    {
        var customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId)
            ?? throw new NotFoundException("Cliente não encontrado.");
        var settings = await settingsService.GetLoyaltyAsync();

        var transactions = context.LoyaltyTransactions.AsNoTracking().Where(t => t.CustomerId == customerId);
        var balance = await transactions.SumAsync(t => t.Points);
        var totalItems = await transactions.CountAsync();
        var page = await transactions
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(t => new LoyaltyTransactionDto(t.Id, t.CreatedAt, t.Type, t.Points, t.Description, t.SaleId, t.User.Name))
            .ToListAsync();

        return new LoyaltyStatementDto(
            customer.Id,
            customer.Name,
            balance,
            settings.Enabled,
            settings.AmountPerPoint,
            new PagedResult<LoyaltyTransactionDto>(page, query.Page, query.PageSize, totalItems));
    }

    public async Task<LoyaltyStatementDto> RedeemAsync(int customerId, RedeemPointsDto dto)
    {
        await redeemValidator.ValidateAndThrowAsync(dto);
        var customer = await FindActiveCustomerAsync(customerId);

        // Serializable: dois resgates do mesmo cliente ao mesmo tempo não conseguem ler o mesmo saldo
        // e gastar os pontos duas vezes. O banco desfaz o segundo e a API responde 409 (tente de novo).
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var balance = await GetBalanceAsync(customerId);
        if (dto.Points > balance)
        {
            throw new BusinessException($"Saldo insuficiente. O cliente tem {balance} ponto(s) e o resgate pede {dto.Points}.");
        }

        Add(customer.Id, -dto.Points, LoyaltyType.REDEEM, dto.Description.Trim());
        auditService.Add(AuditAction.LOYALTY_REDEEMED, nameof(Customer), customer.Id, customer.Name,
            new { balance }, new { balance = balance - dto.Points, redeemed = dto.Points, description = dto.Description.Trim() });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetStatementAsync(customerId, new PagedQuery());
    }

    public async Task<LoyaltyStatementDto> AdjustAsync(int customerId, AdjustPointsDto dto)
    {
        await adjustValidator.ValidateAndThrowAsync(dto);
        var customer = await FindActiveCustomerAsync(customerId);

        // Mesmo cuidado do resgate: o saldo lido não pode mudar até o ajuste ser gravado.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var balance = await GetBalanceAsync(customerId);
        if (balance + dto.Points < 0)
        {
            throw new BusinessException($"O ajuste deixaria o saldo negativo. O cliente tem {balance} ponto(s).");
        }

        Add(customer.Id, dto.Points, LoyaltyType.ADJUSTMENT, dto.Reason.Trim());
        auditService.Add(AuditAction.LOYALTY_ADJUSTED, nameof(Customer), customer.Id, customer.Name,
            new { balance }, new { balance = balance + dto.Points, reason = dto.Reason.Trim() });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetStatementAsync(customerId, new PagedQuery());
    }

    public async Task<int> AddPointsForSaleAsync(Sale sale)
    {
        if (sale.CustomerId is null)
        {
            return 0;
        }

        var settings = await settingsService.GetLoyaltyAsync();
        if (!settings.Enabled)
        {
            return 0;
        }

        // Sempre para baixo: R$ 59,90 com R$ 10 por ponto = 5 pontos (os R$ 9,90 que sobram não viram ponto).
        var points = (int)Math.Floor(sale.Total / settings.AmountPerPoint);
        if (points > 0)
        {
            Add(sale.CustomerId.Value, points, LoyaltyType.EARN, $"Venda #{sale.Id}", sale.Id);
        }

        return points;
    }

    public async Task ReversePointsForSaleAsync(Sale sale)
    {
        // Estorna exatamente o que a venda deu (mesmo que a regra tenha mudado depois).
        var earned = await context.LoyaltyTransactions
            .Where(t => t.SaleId == sale.Id && t.Type == LoyaltyType.EARN)
            .SumAsync(t => t.Points);

        // Se o cliente já gastou esses pontos, o saldo pode ficar negativo: é uma "dívida" de pontos,
        // que as próximas compras cobrem. Bloquear o cancelamento por causa disso seria pior.
        if (earned > 0 && sale.CustomerId.HasValue)
        {
            Add(sale.CustomerId.Value, -earned, LoyaltyType.ADJUSTMENT, $"Estorno: venda #{sale.Id} cancelada", sale.Id);
        }
    }

    private void Add(int customerId, int points, LoyaltyType type, string description, int? saleId = null)
    {
        context.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            CustomerId = customerId,
            Points = points,
            Type = type,
            Description = description,
            SaleId = saleId,
            UserId = currentUser.UserId,
        });
    }

    private async Task<int> GetBalanceAsync(int customerId) =>
        await context.LoyaltyTransactions.Where(t => t.CustomerId == customerId).SumAsync(t => t.Points);

    private async Task<Customer> FindActiveCustomerAsync(int customerId)
    {
        var customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId)
            ?? throw new NotFoundException("Cliente não encontrado.");

        if (!customer.Active)
        {
            throw new BusinessException($"O cliente \"{customer.Name}\" está inativo.");
        }

        return customer;
    }
}
