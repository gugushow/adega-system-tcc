namespace AdegaSystem.Api.Entities;

/// <summary>
/// Configuração do sistema guardada no banco como chave/valor (ex.: "MinimumMarginPercent" = "15").
/// Chave/valor evita criar uma coluna nova a cada configuração: a próxima (pontos de fidelidade,
/// na Fase 7) é só mais uma linha. O valor fica como texto e cada service converte para o tipo certo.
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
