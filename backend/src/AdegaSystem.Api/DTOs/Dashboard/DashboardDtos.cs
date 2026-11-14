using AdegaSystem.Api.DTOs.Reports;

namespace AdegaSystem.Api.DTOs.Dashboard;

/// <summary>Os números dos cards do dashboard. Só vendas concluídas entram em vendas, faturamento e lucro.</summary>
public record DashboardCardsDto(
    int SalesToday,
    decimal RevenueToday,
    decimal RevenueMonth,
    decimal ProfitMonth,
    int ActiveProducts,
    int TotalItemsInStock,
    int LowStockProducts,
    int OutOfStockProducts,
    int ExpiringProducts,
    int ExpiredProducts,
    int ExpiringDays);

/// <summary>Tudo o que a tela inicial do ADMIN mostra, numa requisição só.</summary>
public record DashboardDto(
    DashboardCardsDto Cards,
    IReadOnlyList<SalesPeriodDto> Last30Days,
    IReadOnlyList<SalesPeriodDto> Last12Months,
    IReadOnlyList<ProductSalesDto> TopProducts,
    IReadOnlyList<CategorySalesDto> TopCategories,
    IReadOnlyList<PaymentMethodSalesDto> PaymentMethodsMonth);
