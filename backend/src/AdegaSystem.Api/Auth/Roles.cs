using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Auth;

/// <summary>
/// Nomes dos perfis para usar em [Authorize(Roles = ...)]. Atributos só aceitam constantes,
/// por isso os nomes ficam aqui (iguais aos do enum UserRole). Vírgula quer dizer "um ou outro".
/// </summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.ADMIN);
    public const string Vendedor = nameof(UserRole.VENDEDOR);
    public const string Estoquista = nameof(UserRole.ESTOQUISTA);

    /// <summary>Estoque, lotes, compras, fornecedores e escrita de produtos/categorias.</summary>
    public const string AdminOrEstoquista = Admin + "," + Estoquista;

    /// <summary>Vendas (PDV) e clientes.</summary>
    public const string AdminOrVendedor = Admin + "," + Vendedor;
}
