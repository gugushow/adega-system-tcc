using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Data.DemoData;

// Cadastros da demonstração. Tudo é fictício: marcas inventadas, e-mails em @adegasystem.local,
// CPFs e CNPJs só com os dígitos verificadores corretos e códigos de barras EAN-13 com prefixo 290.
// A faixa 200-299 é reservada pela GS1 para uso interno das lojas: nenhum produto de mercado usa.
// Cada registro tem uma "Key" curta, usada pela linha do tempo (DemoTimeline) para achar o Id.

public record DemoSupplier(string Key, string Name, string CorporateName, string Cnpj, string Phone, string Email, string Address);

public record DemoProduct(
    string Key, string Name, string Brand, string Category, string? Volume, decimal? AlcoholContent,
    decimal SalePrice, int MinimumStock, string? Barcode);

public record DemoCustomer(string Key, string Name, string? Cpf = null, string? Phone = null, string? Email = null, DateOnly? BirthDate = null);

public record DemoUser(string Key, string Name, string Email, UserRole Role);

public record DemoComboItem(string Product, int Quantity);

public record DemoCombo(string Key, string Name, string Description, decimal SalePrice, DemoComboItem[] Items);

public static class DemoCatalog
{
    /// <summary>Categorias que faltam no seed da migration. As outras 10 são reaproveitadas pelo nome.</summary>
    public static readonly (string Name, string Description)[] NewCategories =
    [
        ("Sucos", "Sucos, néctares e bebidas de fruta"),
        ("Cachaças", "Cachaças brancas, envelhecidas e artesanais"),
    ];

    public static readonly DemoSupplier[] Suppliers =
    [
        new("serra-azul", "Distribuidora Serra Azul", "Distribuidora Serra Azul Ltda.", "91304100000167",
            "(11) 3000-0001", "compras.serraazul@adegasystem.local", "Rua das Cervejas, 100 - Centro, Cidade Demo/SP"),
        new("central", "Bebidas Central", "Bebidas Central Ltda.", "92608200000140",
            "(11) 3000-0002", "vendas.central@adegasystem.local", "Av. dos Refrigerantes, 2000 - Distrito Industrial, Cidade Demo/SP"),
        new("vila-norte", "Comercial Vila Norte", "Comercial Vila Norte Ltda.", "93912300000128",
            "(11) 3000-0003", "pedidos.vilanorte@adegasystem.local", "Rua do Comércio, 45 - Vila Norte, Cidade Demo/SP"),
        new("premium", "Distribuidora Premium", "Distribuidora Premium Ltda.", "94216400000182",
            "(11) 3000-0004", "comercial.premium@adegasystem.local", "Av. Paulista Demo, 900 - Centro, Cidade Demo/SP"),
        new("nova-era", "Atacado Nova Era", "Atacado Nova Era Ltda.", "95520500000160",
            "(11) 3000-0005", "atendimento.novaera@adegasystem.local", "Rodovia Demo, km 12 - Cidade Demo/SP"),
        new("golden-drinks", "Importadora Golden Drinks", "Importadora Golden Drinks Ltda.", "96824600000143",
            "(11) 3000-0006", "importacao.golden@adegasystem.local", "Rua do Porto, 300 - Cidade Demo/SP"),
        // Fica inativo no fim da carga, mas com compras no histórico.
        new("vale-do-sol", "Vinícola Vale do Sol", "Vinícola Vale do Sol Ltda.", "97128700000106",
            "(54) 3000-0007", "vendas.valedosol@adegasystem.local", "Estrada do Vinhedo, s/n - Vale Demo/RS"),
        // Cadastrado e sem nenhuma compra.
        new("boa-safra", "Empório Boa Safra", "Empório Boa Safra Ltda.", "98432800000186",
            "(11) 3000-0008", "contato.boasafra@adegasystem.local", "Rua das Flores, 8 - Cidade Demo/SP"),
    ];

    public static readonly DemoProduct[] Products =
    [
        // Cervejas
        new("pilsen-lata", "Cerveja Pilsen Serra Alta Lata 350ml", "Serra Alta", "Cerveja", "350ml", 4.5m, 4.99m, 48, "2900000000018"),
        new("pilsen-garrafa", "Cerveja Pilsen Serra Alta Garrafa 600ml", "Serra Alta", "Cerveja", "600ml", 4.5m, 9.90m, 24, "2900000000025"),
        new("puro-malte", "Cerveja Puro Malte Imperial Long Neck 330ml", "Imperial", "Cerveja", "330ml", 5.0m, 6.49m, 36, "2900000000032"),
        new("ipa", "Cerveja IPA Monte Verde 500ml", "Monte Verde", "Cerveja", "500ml", 6.5m, 16.90m, 12, "2900000000049"),
        new("weiss", "Cerveja Weiss Aurora Norte 500ml", "Aurora Norte", "Cerveja", "500ml", 5.2m, 14.90m, 12, "2900000000056"),
        new("lager-lata", "Cerveja Lager Imperial Lata 473ml", "Imperial", "Cerveja", "473ml", 4.8m, 6.29m, 48, "2900000000063"),
        new("sem-alcool", "Cerveja Sem Álcool Serra Alta Lata 350ml", "Serra Alta", "Cerveja", "350ml", 0m, 5.49m, 12, "2900000000070"),
        new("stout", "Cerveja Stout Artesanal Golden Oak 500ml", "Golden Oak", "Cerveja", "500ml", 6.0m, 18.90m, 6, null),

        // Refrigerantes
        new("cola-2l", "Refrigerante Cola Prime 2L", "Prime", "Refrigerante", "2L", null, 11.99m, 24, "2900000000087"),
        new("cola-lata", "Refrigerante Cola Prime Lata 350ml", "Prime", "Refrigerante", "350ml", null, 5.49m, 36, "2900000000094"),
        new("guarana-2l", "Refrigerante Guaraná Vila Real 2L", "Vila Real", "Refrigerante", "2L", null, 10.49m, 24, "2900000000100"),
        new("limao-2l", "Refrigerante Limão Vila Real 2L", "Vila Real", "Refrigerante", "2L", null, 9.99m, 12, "2900000000117"),
        new("tonica", "Água Tônica Prime Lata 350ml", "Prime", "Refrigerante", "350ml", null, 5.99m, 24, "2900000000124"),

        // Água
        new("agua-500", "Água Mineral sem Gás Fonte Clara 500ml", "Fonte Clara", "Água", "500ml", null, 2.99m, 48, "2900000000131"),
        new("agua-gas", "Água Mineral com Gás Fonte Clara 500ml", "Fonte Clara", "Água", "500ml", null, 3.49m, 24, "2900000000148"),
        new("agua-5l", "Água Mineral Fonte Clara 5L", "Fonte Clara", "Água", "5L", null, 12.90m, 6, "2900000000155"),

        // Energéticos
        new("energetico-473", "Energético Blue Mountain Lata 473ml", "Blue Mountain", "Energético", "473ml", null, 12.90m, 24, "2900000000162"),
        new("energetico-250", "Energético Blue Mountain Lata 250ml", "Blue Mountain", "Energético", "250ml", null, 8.90m, 24, "2900000000179"),
        new("energetico-2l", "Energético Volt Prime 2L", "Prime", "Energético", "2L", null, 19.90m, 6, "2900000000186"),

        // Sucos
        new("suco-uva", "Suco de Uva Integral Vila Real 1L", "Vila Real", "Sucos", "1L", null, 14.90m, 12, "2900000000193"),
        new("suco-laranja", "Suco de Laranja Monte Verde 1L", "Monte Verde", "Sucos", "1L", null, 9.90m, 12, "2900000000209"),
        new("suco-pessego", "Néctar de Pêssego Monte Verde Lata 335ml", "Monte Verde", "Sucos", "335ml", null, 4.99m, 12, "2900000000216"),

        // Vinhos
        new("vinho-cabernet", "Vinho Tinto Cabernet Sauvignon Vila Real 750ml", "Vila Real", "Vinho", "750ml", 13.0m, 59.90m, 6, "2900000000223"),
        new("vinho-malbec", "Vinho Tinto Malbec Aurora Norte 750ml", "Aurora Norte", "Vinho", "750ml", 13.5m, 79.90m, 6, "2900000000230"),
        new("vinho-branco", "Vinho Branco Chardonnay Vila Real 750ml", "Vila Real", "Vinho", "750ml", 12.5m, 54.90m, 6, "2900000000247"),
        new("vinho-rose", "Vinho Rosé Aurora Norte 750ml", "Aurora Norte", "Vinho", "750ml", 12.0m, 49.90m, 4, "2900000000254"),
        new("espumante", "Espumante Brut Serra Alta 750ml", "Serra Alta", "Vinho", "750ml", 11.5m, 69.90m, 6, "2900000000261"),

        // Vodkas
        new("vodka-prime", "Vodka Prime 1L", "Prime", "Vodka", "1L", 37.5m, 39.90m, 6, "2900000000278"),
        new("vodka-premium", "Vodka Premium Blue Mountain 750ml", "Blue Mountain", "Vodka", "750ml", 40.0m, 89.90m, 4, "2900000000285"),
        new("vodka-ice", "Vodka Ice Limão Prime 275ml", "Prime", "Vodka", "275ml", 5.0m, 8.99m, 24, "2900000000292"),

        // Whiskies
        new("whisky-8", "Whisky Golden Oak 8 Anos 1L", "Golden Oak", "Whisky", "1L", 40.0m, 119.90m, 4, "2900000000308"),
        new("whisky-12", "Whisky Golden Oak 12 Anos 750ml", "Golden Oak", "Whisky", "750ml", 40.0m, 189.90m, 3, "2900000000315"),
        new("whisky-honey", "Whisky Honey Blue Mountain 750ml", "Blue Mountain", "Whisky", "750ml", 35.0m, 129.90m, 3, "2900000000322"),
        new("bourbon", "Bourbon Imperial Reserve 750ml", "Imperial", "Whisky", "750ml", 45.0m, 159.90m, 2, "2900000000339"),

        // Gins
        new("gin-london", "Gin London Dry Aurora Norte 750ml", "Aurora Norte", "Gin", "750ml", 40.0m, 99.90m, 4, "2900000000346"),
        new("gin-pink", "Gin Pink Aurora Norte 750ml", "Aurora Norte", "Gin", "750ml", 37.5m, 109.90m, 4, "2900000000353"),
        new("gin-vila-real", "Gin Vila Real 1L", "Vila Real", "Gin", "1L", 37.5m, 64.90m, 4, "2900000000360"),

        // Cachaças
        new("cachaca-prata", "Cachaça Prata Serra Alta 1L", "Serra Alta", "Cachaças", "1L", 39.0m, 24.90m, 6, "2900000000377"),
        new("cachaca-ouro", "Cachaça Ouro Envelhecida Monte Verde 700ml", "Monte Verde", "Cachaças", "700ml", 40.0m, 59.90m, 4, "2900000000384"),
        new("cachaca-artesanal", "Cachaça Artesanal Vila Real 600ml", "Vila Real", "Cachaças", "600ml", 42.0m, 44.90m, 4, null),

        // Gelo e outros (itens sem código de barras, como na vida real)
        new("gelo", "Gelo em Cubos Cristal 5kg", "Cristal", "Gelo", "5kg", null, 14.90m, 20, null),
        new("carvao", "Carvão Vegetal Brasa Forte 3kg", "Brasa Forte", "Outros", "3kg", null, 19.90m, 10, null),
        new("copo", "Copo Descartável Festa Fácil 300ml (50 un.)", "Festa Fácil", "Outros", "300ml", null, 7.90m, 10, null),
        new("amendoim", "Amendoim Torrado Serra Alta 150g", "Serra Alta", "Outros", "150g", null, 6.90m, 12, "2900000000391"),
    ];

    /// <summary>Produtos que ficam inativos no fim da carga (depois de terem compras e vendas).</summary>
    public static readonly string[] ProductsToDeactivate = ["stout", "cachaca-artesanal"];

    public static readonly DemoCustomer[] Customers =
    [
        new("c01", "Mariana Costa Lima", "90173701302", "(11) 90000-0001", "mariana.lima@adegasystem.local", new DateOnly(1990, 3, 14)),
        new("c02", "Rafael Oliveira Santos", "90247402664", "(11) 90000-0002"),
        new("c03", "Juliana Pereira Rocha", "90311103960", "(11) 90000-0003", "juliana.rocha@adegasystem.local"),
        new("c04", "Thiago Almeida Souza", "90484805231", "(11) 90000-0004"),
        new("c05", "Camila Ferreira Gomes", "90558506593", "(11) 90000-0005", BirthDate: new DateOnly(1988, 11, 2)),
        new("c06", "Lucas Martins Ribeiro", "90622207890", "(11) 90000-0006"),
        new("c07", "Fernanda Carvalho Dias", "90795909160", "(11) 90000-0007", "fernanda.dias@adegasystem.local"),
        new("c08", "Gabriel Barbosa Melo", "90869610457", "(11) 90000-0008"),
        new("c09", "Beatriz Nunes Teixeira", "90933311753", "(11) 90000-0009", BirthDate: new DateOnly(1995, 7, 21)),
        new("c10", "Pedro Henrique Moura", "91007013001", "(11) 90000-0010"),
        new("c11", "Larissa Cardoso Pinto", "91170714374"),
        new("c12", "Diego Araújo Freitas", "91244415626", Email: "diego.freitas@adegasystem.local"),
        new("c13", "Patrícia Lopes Mendes", "91318116988", "(11) 90000-0013"),
        // Fica inativo no fim da carga, com compras no histórico.
        new("c14", "Rodrigo Castro Vieira", "91481818201", "(11) 90000-0014"),
        new("c15", "Aline Monteiro Duarte", "91555519555"),
        new("c16", "Bruno Correia Farias", Phone: "(11) 90000-0016"),
        new("c17", "Vanessa Ramos Cunha", Phone: "(11) 90000-0017"),
        new("c18", "Marcelo Batista Reis", Phone: "(11) 90000-0018"),
        new("c19", "Tatiane Moreira Campos", Email: "tatiane.campos@adegasystem.local"),
        new("c20", "Felipe Azevedo Prado", Phone: "(11) 90000-0020"),
        new("c21", "Renata Fonseca Leal", Phone: "(11) 90000-0021"),
        new("c22", "André Siqueira Brito"),
        new("c23", "Priscila Tavares Lacerda"),
        new("c24", "Eduardo Peixoto Valente", Phone: "(11) 90000-0024"),
        new("c25", "Natália Rezende Coelho"),
    ];

    public const string CustomerToDeactivate = "c14";

    /// <summary>O admin é o admin@adega.local da migration (Id 1); aqui entram só os outros perfis.</summary>
    public static readonly DemoUser[] Users =
    [
        new("vendedor1", "Vendedor Demo 1", "vendedor1@adegasystem.local", UserRole.VENDEDOR),
        new("vendedor2", "Vendedora Demo 2", "vendedor2@adegasystem.local", UserRole.VENDEDOR),
        new("estoquista1", "Estoquista Demo 1", "estoquista1@adegasystem.local", UserRole.ESTOQUISTA),
        new("estoquista2", "Estoquista Demo 2", "estoquista2@adegasystem.local", UserRole.ESTOQUISTA),
    ];

    /// <summary>Preço de cada combo um pouco abaixo da soma dos produtos (a regra do sistema exige isso).</summary>
    public static readonly DemoCombo[] Combos =
    [
        new("churrasco", "Combo Churrasco", "12 latas de Pilsen, 2 sacos de gelo e 1 carvão", 99.90m,
            [new("pilsen-lata", 12), new("gelo", 2), new("carvao", 1)]),
        new("festa", "Combo Festa", "Vodka, 4 energéticos, gelo e copos", 104.90m,
            [new("vodka-prime", 1), new("energetico-473", 4), new("gelo", 1), new("copo", 1)]),
        new("whisky", "Combo Whisky", "Whisky 8 anos, 2 energéticos e gelo", 149.90m,
            [new("whisky-8", 1), new("energetico-473", 2), new("gelo", 1)]),
        new("gin", "Combo Gin", "Gin London Dry, 6 tônicas e gelo", 139.90m,
            [new("gin-london", 1), new("tonica", 6), new("gelo", 1)]),
        new("vinho", "Combo Vinho", "Um tinto Cabernet e um branco Chardonnay", 104.90m,
            [new("vinho-cabernet", 1), new("vinho-branco", 1)]),
        new("sem-alcool", "Combo Sem Álcool", "6 cervejas sem álcool, refrigerante e suco de uva", 54.90m,
            [new("sem-alcool", 6), new("cola-2l", 1), new("suco-uva", 1)]),
        new("happy-hour", "Combo Happy Hour", "6 long necks e 2 amendoins", 47.90m,
            [new("puro-malte", 6), new("amendoim", 2)]),
        // Fica inativo no fim da carga.
        new("caipirinha", "Combo Caipirinha", "Cachaça, gelo e refrigerante de limão", 44.90m,
            [new("cachaca-prata", 1), new("gelo", 1), new("limao-2l", 1)]),
    ];

    public const string ComboToDeactivate = "caipirinha";
    public const string SupplierToDeactivate = "vale-do-sol";
}
