using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Data.DemoData;

// Roteiro da demonstração: o que aconteceu em cada dia. "Day" é relativo ao dia em que a carga
// roda (0 = hoje, -30 = 30 dias atrás), então os cenários de validade sempre fazem sentido.
// O DemoDataSeeder executa tudo em ordem cronológica, sempre pelos services reais.

/// <summary>Um item de compra. Validade relativa a hoje (null = sem validade, ex.: destilados e gelo).</summary>
public record DemoPurchaseItem(string Product, int Quantity, decimal UnitCost, int? ExpiresInDays = null);

public record DemoPurchase(int Day, string Supplier, string Invoice, DemoPurchaseItem[] Items);

public record DemoSaleItem(string Product, int Quantity);

public record DemoSaleCombo(string Combo, int Quantity);

/// <summary>Uma venda. Seller: "admin" ou a Key de um usuário demo. CancelReason preenchido = cancelada pelo ADMIN 45 min depois.</summary>
public record DemoSale(
    int Day, string Time, string Seller, string? Customer, PaymentMethod Payment, decimal Discount,
    DemoSaleItem[] Items, DemoSaleCombo[]? Combos = null, string? CancelReason = null);

/// <summary>Perda registrada pelo ajuste de estoque (tipo LOSS), sempre no lote que vence primeiro.</summary>
public record DemoLoss(int Day, string Time, string Product, int Quantity, string Reason);

public static class DemoTimeline
{
    // Dias dos cadastros: tudo existe antes da primeira compra (D-88).
    public const int CatalogDay = -100;
    public const int CustomersDay = -95;
    public const int CombosDay = -26;
    public const int DeactivationsDay = -1;

    public static readonly DemoPurchase[] Purchases =
    [
        new(-88, "serra-azul", "DEMO-1001",
        [
            new("pilsen-lata", 120, 2.79m, ExpiresInDays: 6),    // lote que vence em até 7 dias
            new("pilsen-garrafa", 48, 5.20m, 120),
            new("puro-malte", 72, 3.45m, 15),                    // vence em até 30 dias
            new("lager-lata", 96, 3.10m, 45),                    // vence em até 60 dias
            new("sem-alcool", 24, 2.95m, 140),
        ]),
        new(-85, "central", "DEMO-2001",
        [
            new("cola-2l", 48, 6.20m, 20),
            new("cola-lata", 96, 2.60m, 50),
            new("guarana-2l", 24, 5.40m, -2),                    // vencido com saldo
            new("limao-2l", 12, 5.10m, 90),
            new("tonica", 48, 2.90m, 40),
        ]),
        new(-82, "vila-norte", "DEMO-3001",
        [
            new("suco-uva", 12, 8.40m, -10),                     // vencido com saldo
            new("suco-laranja", 12, 5.20m, -6),                  // vencido, recebe a baixa em D-2
            new("suco-pessego", 12, 2.40m, 120),
            new("gelo", 40, 6.50m),
            new("carvao", 20, 11.00m),
            new("copo", 30, 3.90m),
            new("amendoim", 30, 3.20m, 150),
        ]),
        new(-80, "premium", "DEMO-4001",
        [
            new("vodka-prime", 24, 19.50m),
            new("vodka-premium", 4, 48.00m),
            new("vodka-ice", 48, 4.30m, 200),
            new("cachaca-prata", 24, 11.20m),
            new("cachaca-ouro", 12, 31.00m),
            new("cachaca-artesanal", 6, 22.00m),
        ]),
        new(-78, "golden-drinks", "DEMO-5001",
        [
            new("whisky-8", 12, 62.00m),
            new("whisky-12", 6, 105.00m),
            new("whisky-honey", 6, 70.00m),
            new("bourbon", 6, 88.00m),
            new("gin-london", 4, 52.00m),
            new("gin-pink", 6, 58.00m),
        ]),
        new(-75, "vale-do-sol", "DEMO-6001",
        [
            new("vinho-cabernet", 12, 29.00m),
            new("vinho-malbec", 12, 41.00m),
            new("vinho-branco", 12, 27.50m),
            new("vinho-rose", 6, 24.00m),
            new("espumante", 12, 34.00m),
        ]),
        new(-72, "central", "DEMO-2002",
        [
            new("agua-500", 120, 0.95m, 300),
            new("agua-gas", 48, 1.20m, 25),
            new("agua-5l", 24, 5.80m, 330),
            new("energetico-473", 48, 6.40m, 5),                 // vence em até 7 dias
            new("energetico-250", 48, 4.30m, 160),
            new("energetico-2l", 6, 10.50m, 200),
        ]),
        new(-65, "nova-era", "DEMO-7001",
        [
            new("weiss", 24, 7.20m, -4),                         // vencido com saldo
            new("ipa", 18, 8.30m, 150),
            new("stout", 12, 9.80m, 180),
            new("gin-vila-real", 6, 33.00m),
        ]),
        new(-60, "serra-azul", "DEMO-1002",
        [
            new("pilsen-lata", 240, 2.89m, 150),
            new("puro-malte", 48, 3.55m, 130),
        ]),
        new(-55, "central", "DEMO-2003",
        [
            new("cola-2l", 48, 6.50m, 70),
            new("guarana-2l", 36, 5.55m, 80),
            new("cola-lata", 48, 2.70m, 110),
        ]),
        new(-50, "vila-norte", "DEMO-3002",
        [
            new("gelo", 40, 6.70m),
            new("carvao", 15, 11.30m),
            new("suco-uva", 24, 8.90m, 28),                      // vence em até 30 dias
            new("copo", 20, 3.95m),
        ]),
        new(-45, "golden-drinks", "DEMO-5002",
        [
            new("whisky-8", 6, 64.50m),
            new("gin-london", 2, 53.50m),
            new("vinho-malbec", 6, 42.00m),
        ]),
        new(-42, "nova-era", "DEMO-7002",
        [
            new("weiss", 24, 7.40m, 120),
            new("lager-lata", 96, 3.20m, 160),
            new("cola-2l", 24, 6.35m, 90),                       // 3º custo da cola: 6,20 → 6,50 → 6,35
        ]),
        new(-35, "premium", "DEMO-4002",
        [
            new("vodka-prime", 12, 20.10m),
            new("cachaca-prata", 12, 11.60m),
            new("vodka-ice", 48, 4.40m, 230),
        ]),
        new(-28, "vale-do-sol", "DEMO-6002",
        [
            new("vinho-cabernet", 12, 29.80m),
            new("espumante", 12, 35.00m),
            new("vinho-branco", 6, 28.00m),
        ]),
        new(-20, "serra-azul", "DEMO-1003",
        [
            new("pilsen-lata", 240, 2.95m, 200),
            new("pilsen-garrafa", 48, 5.35m, 170),
            new("sem-alcool", 24, 3.05m, 180),
        ]),
        new(-14, "central", "DEMO-2004",
        [
            new("agua-500", 240, 0.98m, 340),
            new("energetico-473", 48, 6.60m, 170),
            new("tonica", 24, 3.00m, 150),
        ]),
        new(-10, "vila-norte", "DEMO-3003",
        [
            new("suco-laranja", 12, 5.40m, 3),                   // vence em até 7 dias
            new("gelo", 60, 6.90m),
            new("amendoim", 24, 3.30m, 160),
        ]),
        new(-6, "golden-drinks", "DEMO-5003",
        [
            new("whisky-honey", 6, 72.00m),
            new("bourbon", 2, 90.00m),
        ]),
        new(-3, "central", "DEMO-2005",
        [
            new("cola-lata", 48, 2.75m, 120),
            new("energetico-250", 24, 4.35m, 165),
            new("agua-gas", 24, 1.25m, 200),
        ]),
    ];

    private const PaymentMethod Pix = PaymentMethod.PIX;
    private const PaymentMethod Cash = PaymentMethod.CASH;
    private const PaymentMethod Credit = PaymentMethod.CREDIT_CARD;
    private const PaymentMethod Debit = PaymentMethod.DEBIT_CARD;

    public static readonly DemoSale[] Sales =
    [
        new(-30, "10:15", "vendedor1", "c01", Pix, 0, [new("pilsen-lata", 12), new("gelo", 2)]),
        new(-29, "18:40", "vendedor2", null, Cash, 0, [new("cola-2l", 2), new("agua-500", 6)]),
        new(-28, "19:05", "vendedor1", "c02", Credit, 0, [new("whisky-8", 1), new("energetico-473", 4)]),
        new(-27, "17:30", "vendedor2", "c03", Debit, 0, [new("vinho-cabernet", 2), new("vinho-branco", 1)]),
        new(-26, "20:10", "vendedor1", null, Pix, 0, [new("puro-malte", 12), new("amendoim", 2), new("suco-pessego", 4)]),
        new(-25, "11:00", "admin", "c04", Credit, 10.00m, [new("gin-london", 1), new("tonica", 6), new("gelo", 1)]),
        new(-24, "19:45", "vendedor1", "c05", Pix, 0, [], [new("churrasco", 1)]),
        new(-23, "21:15", "vendedor2", null, Cash, 0, [new("vodka-prime", 1), new("energetico-473", 2), new("gelo", 1)]),
        new(-22, "16:20", "vendedor1", "c06", Debit, 0, [new("espumante", 2), new("cola-lata", 6)]),
        new(-21, "18:00", "vendedor2", "c01", Pix, 0, [new("pilsen-lata", 6)], [new("festa", 1)]),
        new(-20, "19:30", "vendedor1", null, Credit, 0, [new("whisky-12", 1), new("agua-gas", 4)]),
        new(-19, "12:10", "vendedor2", "c07", Pix, 0, [new("cachaca-prata", 2), new("limao-2l", 2), new("gelo", 2), new("cachaca-artesanal", 1)]),
        new(-18, "20:45", "vendedor1", "c08", Credit, 5.00m, [], [new("whisky", 1)]),
        new(-17, "17:10", "vendedor2", null, Cash, 0, [new("pilsen-garrafa", 6), new("carvao", 1), new("stout", 2)]),
        new(-16, "19:20", "vendedor1", "c09", Pix, 0, [new("vinho-malbec", 2), new("vinho-rose", 1)]),
        new(-15, "21:30", "admin", "c02", Credit, 0, [], [new("gin", 1)]),
        new(-14, "15:40", "vendedor2", "c10", Debit, 0, [new("energetico-250", 6), new("vodka-ice", 6)]),
        new(-13, "18:50", "vendedor1", null, Pix, 0, [new("weiss", 6), new("ipa", 4)]),
        new(-12, "20:05", "vendedor2", "c11", Credit, 0, [new("gin-pink", 1), new("tonica", 4)]),
        new(-11, "11:30", "vendedor1", "c03", Pix, 0, [new("agua-5l", 1), new("suco-pessego", 6)], [new("sem-alcool", 1)]),
        new(-10, "19:00", "vendedor2", null, Cash, 0, [new("lager-lata", 12), new("gelo", 2)]),
        new(-9, "17:45", "vendedor1", "c12", Credit, 0, [new("whisky-honey", 1), new("bourbon", 1)],
            CancelReason: "Cliente desistiu da compra (demo)"),
        new(-9, "20:30", "vendedor1", "c12", Debit, 0, [new("whisky-honey", 1)]),
        new(-8, "18:15", "vendedor2", "c14", Pix, 0, [], [new("happy-hour", 2)]),
        new(-7, "19:40", "vendedor1", "c05", Credit, 0, [new("vodka-premium", 2), new("energetico-473", 4)]),
        new(-6, "21:00", "vendedor2", null, Pix, 0, [new("suco-laranja", 2), new("suco-uva", 2), new("agua-500", 12)]),
        new(-5, "18:20", "vendedor1", "c15", Debit, 4.90m, [], [new("vinho", 1)]),
        new(-4, "20:50", "admin", "c06", Credit, 0, [new("whisky-8", 1), new("ipa", 6)]),
        new(-3, "17:00", "vendedor2", "c01", Pix, 0, [new("energetico-2l", 3), new("gelo", 2)]),
        new(-3, "19:30", "vendedor1", null, Cash, 0, [new("pilsen-lata", 24)]),
        new(-2, "18:45", "vendedor2", "c09", Pix, 0, [new("gin-london", 1), new("vinho-rose", 2)]),
        new(-1, "20:10", "vendedor1", "c04", Credit, 0, [new("vodka-premium", 2), new("energetico-2l", 3), new("gin-pink", 1)]),
        new(0, "10:30", "vendedor2", null, Pix, 0, [new("agua-500", 6), new("cola-lata", 6)]),
        new(0, "14:15", "vendedor1", "c07", Debit, 0, [new("limao-2l", 1)], [new("churrasco", 1)]),
        new(0, "17:40", "vendedor2", "c10", Pix, 0, [new("pilsen-lata", 12), new("suco-laranja", 2)]),
        new(0, "19:20", "admin", "c11", Credit, 0, [new("whisky-12", 2)]),
    ];

    public static readonly DemoLoss[] Losses =
    [
        new(-12, "09:30", "suco-pessego", 2, "Latas amassadas no transporte (demo)"),
        new(-8, "10:00", "espumante", 1, "Garrafa quebrada no estoque (demo)"),
    ];

    /// <summary>Baixa de lote vencido (movimento EXPIRED), feita pelo estoquista.</summary>
    public static readonly (int Day, string Time, string Product) WriteOff = (-2, "09:15", "suco-laranja");

    /// <summary>Resgate de pontos de fidelidade, no balcão.</summary>
    public static readonly (int Day, string Time, string Customer, int Points, string Description) Redemption =
        (-2, "18:00", "c01", 10, "Troca por um copo personalizado (demo)");
}
