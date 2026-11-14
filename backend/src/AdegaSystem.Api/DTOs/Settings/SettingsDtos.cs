namespace AdegaSystem.Api.DTOs.Settings;

/// <summary>Margem mínima (%) que todo produto deveria ter. Abaixo dela, o sistema mostra um alerta.</summary>
public record MarginSettingsDto(decimal MinimumMarginPercent);

/// <summary>
/// Programa de fidelidade. Enabled = se as vendas geram pontos.
/// AmountPerPoint = quantos reais o cliente precisa gastar para ganhar 1 ponto (ex.: 10 → R$ 10,00 = 1 ponto).
/// </summary>
public record LoyaltySettingsDto(bool Enabled, decimal AmountPerPoint);
