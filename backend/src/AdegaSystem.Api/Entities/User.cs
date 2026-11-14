namespace AdegaSystem.Api.Entities;

/// <summary>
/// Usuário do sistema: quem faz login e quem fica registrado em cada compra, venda e movimentação.
/// O Role define o que ele pode fazer (ver Auth/Roles.cs).
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash da senha (nunca a senha em texto). Vazio no admin semeado até a senha inicial ser configurada.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
