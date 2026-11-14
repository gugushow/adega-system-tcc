namespace AdegaSystem.Api.Auth;

/// <summary>
/// Nomes das informações (claims) gravadas no token. Usamos os nomes curtos do padrão JWT
/// e desligamos o "mapeamento" do ASP.NET (MapInboundClaims = false), então o que está
/// no token é exatamente o que o código lê.
/// </summary>
public static class AppClaims
{
    public const string UserId = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
}
