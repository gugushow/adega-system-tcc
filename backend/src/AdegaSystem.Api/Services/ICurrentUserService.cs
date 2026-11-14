namespace AdegaSystem.Api.Services;

/// <summary>Quem está fazendo a operação (o usuário do token). Compras, vendas e movimentações gravam esse Id.</summary>
public interface ICurrentUserService
{
    int UserId { get; }
}
