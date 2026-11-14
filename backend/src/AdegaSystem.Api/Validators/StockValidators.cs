using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Entities;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

public class StockAdjustmentDtoValidator : AbstractValidator<StockAdjustmentDto>
{
    public StockAdjustmentDtoValidator()
    {
        RuleFor(x => x.BatchId).GreaterThan(0).WithMessage("Selecione o lote.");

        // Entradas por compra têm tela própria, vencidos têm a baixa de lote e vendas saem pelo PDV.
        RuleFor(x => x.Type)
            .Must(type => type is MovementType.ADJUSTMENT or MovementType.LOSS)
            .WithMessage("Tipo inválido. Use ADJUSTMENT (ajuste) ou LOSS (perda).");

        RuleFor(x => x.Quantity).NotEqual(0).WithMessage("A quantidade não pode ser zero.").WithinLimit();
        RuleFor(x => x.Quantity).LessThan(0)
            .When(x => x.Type == MovementType.LOSS)
            .WithMessage("Uma perda sempre retira unidades: informe a quantidade negativa.");

        RuleFor(x => x.Reason).NotEmpty().WithMessage("Informe o motivo.")
            .MaximumLength(255).WithMessage("O motivo deve ter no máximo 255 caracteres.");
    }
}
