using AdegaSystem.Api.DTOs.Reports;
using FluentValidation;

namespace AdegaSystem.Api.Validators;

/// <summary>Regras de período que valem para todos os relatórios.</summary>
public static class ReportPeriodRules
{
    /// <summary>Até 2 anos por consulta: o suficiente para comparar meses sem carregar o histórico inteiro.</summary>
    public const int MaxDays = 731;

    public static void AddPeriodRules<T>(this AbstractValidator<T> validator) where T : ReportPeriodQuery
    {
        validator.RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("A data final deve ser igual ou posterior à data inicial.")
            .When(x => x.From.HasValue && x.To.HasValue);
        validator.RuleFor(x => x)
            .Must(x => x.To!.Value.DayNumber - x.From!.Value.DayNumber < MaxDays)
            .WithName("to")
            .WithMessage("O período pode ter no máximo 2 anos.")
            .When(x => x.From.HasValue && x.To.HasValue && x.To >= x.From);
    }
}

public class ReportPeriodQueryValidator : AbstractValidator<ReportPeriodQuery>
{
    public ReportPeriodQueryValidator() => this.AddPeriodRules();
}

public class SalesReportQueryValidator : AbstractValidator<SalesReportQuery>
{
    public SalesReportQueryValidator()
    {
        this.AddPeriodRules();
        RuleFor(x => x.GroupBy).IsInEnum().WithMessage("Agrupamento inválido.");
    }
}

public class ProductRankingQueryValidator : AbstractValidator<ProductRankingQuery>
{
    public ProductRankingQueryValidator()
    {
        this.AddPeriodRules();
        RuleFor(x => x.Order).IsInEnum().WithMessage("Ordem inválida.");
        RuleFor(x => x.Limit).InclusiveBetween(1, 100).WithMessage("O limite deve ficar entre 1 e 100.");
    }
}

public class IdleProductsQueryValidator : AbstractValidator<IdleProductsQuery>
{
    public IdleProductsQueryValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 365).WithMessage("Informe de 1 a 365 dias.");
    }
}
