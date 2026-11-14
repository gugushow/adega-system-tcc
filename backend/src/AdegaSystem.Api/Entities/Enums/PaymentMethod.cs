namespace AdegaSystem.Api.Entities;

/// <summary>Forma de pagamento da venda. Gravada como texto no banco ("PIX", "CASH"...).</summary>
public enum PaymentMethod
{
    PIX,
    CASH,
    CREDIT_CARD,
    DEBIT_CARD,
}
