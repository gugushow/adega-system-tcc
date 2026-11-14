using AdegaSystem.Api.Auth;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Lê o usuário logado do token JWT da requisição (claim "sub").
/// Até a Fase 4 esta classe devolvia sempre o admin semeado; como todos os services dependem
/// só da interface ICurrentUserService, nenhum deles precisou mudar com a chegada do login.
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public int UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst(AppClaims.UserId)?.Value;

            // Todo endpoint exige login (ver a FallbackPolicy no Program.cs), então chegar aqui
            // sem usuário é erro de programação, não do usuário.
            return int.TryParse(value, out var id)
                ? id
                : throw new InvalidOperationException("Não há usuário logado nesta requisição.");
        }
    }
}
