using System.Linq.Expressions;
using AdegaSystem.Api.Common;
using AdegaSystem.Api.Common.Exceptions;
using AdegaSystem.Api.Data;
using AdegaSystem.Api.DTOs.Sales;
using AdegaSystem.Api.DTOs.Stock;
using AdegaSystem.Api.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AdegaSystem.Api.Services;

/// <summary>
/// Vendas do PDV. A venda nunca mexe no estoque por conta própria: ela decide de quais lotes
/// sai cada unidade (FEFO) e pede ao StockService para dar a baixa, lote por lote.
/// Combos são abertos em um item por produto; os pontos de fidelidade são dados (e estornados no
/// cancelamento) dentro da mesma transação.
/// </summary>
public class SaleService(
    AdegaDbContext context,
    ICurrentUserService currentUser,
    IStockService stockService,
    ILoyaltyService loyaltyService,
    IAuditService auditService,
    IValidator<CreateSaleDto> createValidator,
    IValidator<CancelSaleDto> cancelValidator) : ISaleService
{
    public async Task<PagedResult<SaleDto>> GetAllAsync(SaleQuery query)
    {
        var sales = context.Sales.AsNoTracking();

        // O dia é o de Brasília (ver BusinessClock). "To" inclui o dia inteiro.
        if (query.From.HasValue)
        {
            var start = BusinessClock.StartOfDayUtc(query.From.Value);
            sales = sales.Where(s => s.SaleDate >= start);
        }

        if (query.To.HasValue)
        {
            var end = BusinessClock.StartOfDayUtc(query.To.Value.AddDays(1));
            sales = sales.Where(s => s.SaleDate < end);
        }

        if (query.Status.HasValue)
        {
            sales = sales.Where(s => s.Status == query.Status.Value);
        }

        if (query.CustomerId.HasValue)
        {
            sales = sales.Where(s => s.CustomerId == query.CustomerId.Value);
        }

        if (query.PaymentMethod.HasValue)
        {
            sales = sales.Where(s => s.PaymentMethod == query.PaymentMethod.Value);
        }

        var totalItems = await sales.CountAsync();

        var page = await sales
            .OrderByDescending(s => s.SaleDate)
            .ThenByDescending(s => s.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync();

        return new PagedResult<SaleDto>(page, query.Page, query.PageSize, totalItems);
    }

    public async Task<SaleDetailsDto> GetByIdAsync(int id)
    {
        var sale = await context.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.User)
            .Include(s => s.CanceledByUser)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Items).ThenInclude(i => i.Combo)
            .Include(s => s.Items).ThenInclude(i => i.Batches).ThenInclude(b => b.Batch)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException("Venda não encontrada.");

        var loyaltyPoints = await context.LoyaltyTransactions
            .Where(t => t.SaleId == id && t.Type == LoyaltyType.EARN)
            .SumAsync(t => t.Points);

        return new SaleDetailsDto(
            sale.Id,
            sale.SaleDate,
            sale.CustomerId,
            sale.Customer?.Name,
            sale.User.Name,
            sale.Subtotal,
            sale.Discount,
            sale.Total,
            sale.TotalCost,
            sale.Profit,
            sale.PaymentMethod,
            sale.Status,
            sale.CanceledAt,
            sale.CanceledByUser?.Name,
            sale.CancelReason,
            loyaltyPoints,
            sale.Items
                .OrderBy(i => i.Id)
                .Select(i => new SaleItemDto(
                    i.Id, i.ProductId, i.Product.Name, i.ComboId, i.Combo?.Name,
                    i.Quantity, i.UnitPrice, i.CostPrice, i.Discount, i.Total,
                    // Mesma ordem em que os lotes foram usados (FEFO).
                    OrderFefo(i.Batches, b => b.Batch)
                        .Select(b => new SaleItemBatchDto(b.BatchId, b.Batch.BatchNumber, b.Batch.ExpirationDate, b.Quantity, b.UnitCost))
                        .ToList()))
                .ToList());
    }

    public async Task<SaleDetailsDto> CreateAsync(CreateSaleDto dto)
    {
        await createValidator.ValidateAndThrowAsync(dto);
        await EnsureCustomerIsActiveAsync(dto.CustomerId);
        var looseItems = dto.Items ?? [];
        var comboInputs = dto.Combos ?? [];

        // Tudo numa transação: se qualquer item falhar, nada é gravado (rollback ao sair sem Commit).
        await using var transaction = await context.Database.BeginTransactionAsync();

        // 1) Combos, produtos e lotes. Produtos e lotes vêm rastreados: são as mesmas instâncias
        //    que o StockService vai alterar. Os produtos dos combos entram na mesma lista.
        var comboIds = comboInputs.Select(c => c.ComboId).ToList();
        var combos = await context.Combos.AsNoTracking()
            .Include(c => c.Items)
            .Where(c => comboIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var productIds = looseItems.Select(i => i.ProductId)
            .Concat(combos.Values.SelectMany(c => c.Items).Select(i => i.ProductId))
            .Distinct()
            .ToList();
        var products = await context.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var batches = await context.ProductBatches
            .Where(b => productIds.Contains(b.ProductId) && b.QuantityAvailable > 0)
            .ToListAsync();

        // 2) Monta a venda e escolhe os lotes de cada item, ainda sem gravar nada.
        var sale = new Sale
        {
            CustomerId = dto.CustomerId,
            UserId = currentUser.UserId,
            PaymentMethod = dto.PaymentMethod,
            Discount = dto.Discount,
        };

        // O mesmo produto pode aparecer avulso e dentro de um combo (ou em dois combos).
        // O BatchPicker anota o que cada lote já deu para esta venda, para não prometer a mesma unidade duas vezes.
        var picker = new BatchPicker(batches, BusinessClock.Today);

        foreach (var input in looseItems)
        {
            var product = GetSellableProduct(products, input.ProductId);

            // O preço vem do cadastro, nunca do frontend.
            var gross = input.Quantity * product.SalePrice;
            if (input.Discount > gross)
            {
                throw new BusinessException($"O desconto de \"{product.Name}\" é maior que o valor do item ({Money.Format(gross)}).");
            }

            sale.Items.Add(BuildItem(product, input.Quantity, input.Discount, comboId: null, picker));
        }

        foreach (var input in comboInputs)
        {
            AddComboItems(sale, input, combos, products, picker);
        }

        // 3) Totais e lucro, sempre calculados aqui.
        sale.Subtotal = sale.Items.Sum(i => i.Total);
        if (sale.Discount > sale.Subtotal)
        {
            throw new BusinessException($"O desconto da venda não pode ser maior que o subtotal ({Money.Format(sale.Subtotal)}).");
        }

        sale.Total = sale.Subtotal - sale.Discount;
        // Custo real: cada unidade pelo custo do lote de onde saiu (não o custo médio arredondado).
        sale.TotalCost = Math.Round(
            sale.Items.SelectMany(i => i.Batches).Sum(b => b.Quantity * b.UnitCost), 2, MidpointRounding.AwayFromZero);
        sale.Profit = sale.Total - sale.TotalCost;

        // 4) Grava a venda (gera o Id, usado no motivo das movimentações) e dá baixa lote por lote.
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        foreach (var itemBatch in sale.Items.SelectMany(i => i.Batches))
        {
            await stockService.RegisterMovementAsync(new StockMovementRequest(
                itemBatch.BatchId, MovementType.SALE, -itemBatch.Quantity, $"Venda #{sale.Id}", nameof(Sale), sale.Id));
        }

        // 5) Pontos de fidelidade (se a venda tem cliente e o programa está ligado).
        await loyaltyService.AddPointsForSaleAsync(sale);
        await context.SaveChangesAsync();

        await transaction.CommitAsync();

        return await GetByIdAsync(sale.Id);
    }

    /// <summary>
    /// Abre o combo em um item por produto. Cada item guarda o preço normal do produto (UnitPrice)
    /// e, como desconto, a parte da economia do combo que coube a ele. Assim a soma dos itens dá
    /// exatamente o preço do combo, e os relatórios de produto e lucro funcionam sem nenhum caso especial.
    ///
    /// Ex.: combo de R$ 199,00 com produtos que somam R$ 220,00 → economia de R$ 21,00, dividida
    /// proporcionalmente: o Jack Daniel's (R$ 150,00 de R$ 220,00) recebe R$ 14,32 de desconto, e assim por diante.
    /// </summary>
    private static void AddComboItems(
        Sale sale, CreateSaleComboDto input, Dictionary<int, Combo> combos, Dictionary<int, Product> products, BatchPicker picker)
    {
        if (!combos.TryGetValue(input.ComboId, out var combo))
        {
            throw new BusinessException($"O combo de código {input.ComboId} não existe.");
        }

        if (!combo.Active)
        {
            throw new BusinessException($"O combo \"{combo.Name}\" está inativo e não pode ser vendido.");
        }

        var lines = combo.Items
            .OrderBy(i => i.Id)
            .Select(i => (Product: GetSellableProduct(products, i.ProductId), Quantity: i.Quantity * input.Quantity))
            .ToList();
        var normalValues = lines.Select(l => l.Quantity * l.Product.SalePrice).ToList();

        var normalTotal = normalValues.Sum();
        var comboTotal = combo.SalePrice * input.Quantity;
        // O preço dos produtos pode ter baixado depois que o combo foi cadastrado.
        if (comboTotal > normalTotal)
        {
            throw new BusinessException(
                $"O combo \"{combo.Name}\" ficou mais caro que os produtos separados ({Money.Format(comboTotal)} contra {Money.Format(normalTotal)}). Revise o preço do combo.");
        }

        var discounts = SplitProportionally(normalTotal - comboTotal, normalValues);
        for (var i = 0; i < lines.Count; i++)
        {
            sale.Items.Add(BuildItem(lines[i].Product, lines[i].Quantity, discounts[i], combo.Id, picker));
        }
    }

    /// <summary>Monta um item: preço do cadastro, desconto informado e os lotes escolhidos por FEFO.</summary>
    private static SaleItem BuildItem(Product product, int quantity, decimal discount, int? comboId, BatchPicker picker)
    {
        var allocation = picker.Pick(product, quantity);
        var itemCost = allocation.Sum(a => a.Quantity * a.Batch.UnitCost);

        return new SaleItem
        {
            ProductId = product.Id,
            ComboId = comboId,
            Quantity = quantity,
            UnitPrice = product.SalePrice,
            // Custo médio ponderado dos lotes usados, com as mesmas 4 casas do custo do lote.
            CostPrice = Math.Round(itemCost / quantity, 4, MidpointRounding.AwayFromZero),
            Discount = discount,
            Total = quantity * product.SalePrice - discount,
            Batches = allocation
                .Select(a => new SaleItemBatch { Batch = a.Batch, Quantity = a.Quantity, UnitCost = a.Batch.UnitCost })
                .ToList(),
        };
    }

    /// <summary>
    /// Divide um valor (em reais) proporcionalmente aos pesos, sem perder nem sobrar centavo.
    /// Arredonda o valor acumulado em cada passo e cada parte é a diferença entre dois acumulados:
    /// as partes nunca ficam negativas e a soma dá exatamente o total.
    /// </summary>
    private static List<decimal> SplitProportionally(decimal total, IReadOnlyList<decimal> weights)
    {
        var weightSum = weights.Sum();
        var parts = new List<decimal>(weights.Count);
        decimal accumulatedWeight = 0, previous = 0;

        foreach (var weight in weights)
        {
            accumulatedWeight += weight;
            var accumulated = Math.Round(total * accumulatedWeight / weightSum, 2, MidpointRounding.AwayFromZero);
            parts.Add(accumulated - previous);
            previous = accumulated;
        }

        return parts;
    }

    private static Product GetSellableProduct(Dictionary<int, Product> products, int productId)
    {
        if (!products.TryGetValue(productId, out var product))
        {
            throw new BusinessException($"O produto de código {productId} não existe.");
        }

        if (!product.Active)
        {
            throw new BusinessException($"O produto \"{product.Name}\" está inativo e não pode ser vendido.");
        }

        return product;
    }

    public async Task<SaleDetailsDto> CancelAsync(int id, CancelSaleDto dto)
    {
        await cancelValidator.ValidateAndThrowAsync(dto);

        await using var transaction = await context.Database.BeginTransactionAsync();

        var sale = await context.Sales
            .Include(s => s.Items).ThenInclude(i => i.Batches)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException("Venda não encontrada.");

        if (sale.Status == SaleStatus.CANCELED)
        {
            throw new BusinessException("Esta venda já foi cancelada.");
        }

        // Devolve cada quantidade ao lote de onde saiu, graças ao SaleItemBatch.
        // Se o lote venceu depois da venda, as unidades voltam para ele mesmo assim
        // e aparecem na lista de vencidos, prontas para a baixa.
        var itemBatches = sale.Items.OrderBy(i => i.Id).SelectMany(i => i.Batches.OrderBy(b => b.Id));
        foreach (var itemBatch in itemBatches)
        {
            await stockService.RegisterMovementAsync(new StockMovementRequest(
                itemBatch.BatchId, MovementType.RETURN, itemBatch.Quantity, $"Cancelamento da venda #{sale.Id}", nameof(Sale), sale.Id));
        }

        sale.Status = SaleStatus.CANCELED;
        sale.CanceledAt = DateTime.UtcNow;
        sale.CanceledByUserId = currentUser.UserId;
        sale.CancelReason = dto.Reason.Trim();

        // Os pontos que a venda deu ao cliente saem do saldo, e o cancelamento entra na auditoria.
        await loyaltyService.ReversePointsForSaleAsync(sale);
        auditService.Add(AuditAction.SALE_CANCELED, nameof(Sale), sale.Id, $"Venda #{sale.Id}",
            new { status = SaleStatus.COMPLETED.ToString(), total = sale.Total },
            new { status = SaleStatus.CANCELED.ToString(), reason = sale.CancelReason });

        // Status é token de concorrência: se duas pessoas cancelarem a mesma venda ao mesmo tempo,
        // a segunda falha aqui e tudo o que ela fez (inclusive as devoluções) é desfeito.
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetByIdAsync(sale.Id);
    }

    /// <summary>
    /// FEFO (First Expired, First Out): usa primeiro o lote que vence primeiro.
    /// Lotes vencidos nunca são vendidos; lotes sem validade (gelo, por exemplo) ficam por último.
    ///
    /// Guarda o que já separou de cada lote nesta venda: se a Heineken aparece avulsa e também
    /// dentro de um combo, o segundo pedido enxerga o lote com as unidades que sobraram.
    /// </summary>
    private sealed class BatchPicker(List<ProductBatch> batches, DateOnly today)
    {
        private readonly Dictionary<int, int> _reserved = [];

        private int Free(ProductBatch batch) => batch.QuantityAvailable - _reserved.GetValueOrDefault(batch.Id);

        public List<(ProductBatch Batch, int Quantity)> Pick(Product product, int quantity)
        {
            var productBatches = batches.Where(b => b.ProductId == product.Id).ToList();
            var sellable = OrderFefo(productBatches.Where(b => b.ExpirationDate == null || b.ExpirationDate >= today), b => b).ToList();

            var available = sellable.Sum(Free);
            if (available < quantity)
            {
                var message = $"Estoque insuficiente para \"{product.Name}\". Disponível para venda: {available}, solicitado: {quantity}.";
                var expired = productBatches.Where(b => b.ExpirationDate < today).Sum(b => b.QuantityAvailable);
                if (expired > 0)
                {
                    message += expired == 1
                        ? " Há 1 unidade vencida, que não pode ser vendida."
                        : $" Há {expired} unidades vencidas, que não podem ser vendidas.";
                }

                throw new BusinessException(message);
            }

            // Vai tirando de cada lote o que ele tem livre, até completar a quantidade pedida.
            var allocation = new List<(ProductBatch Batch, int Quantity)>();
            var remaining = quantity;
            foreach (var batch in sellable)
            {
                var take = Math.Min(Free(batch), remaining);
                if (take == 0)
                {
                    continue;
                }

                allocation.Add((batch, take));
                _reserved[batch.Id] = _reserved.GetValueOrDefault(batch.Id) + take;
                remaining -= take;
                if (remaining == 0)
                {
                    break;
                }
            }

            return allocation;
        }
    }

    /// <summary>Vence primeiro, sai primeiro. A mesma ordem da tela de Lotes.</summary>
    private static IEnumerable<T> OrderFefo<T>(IEnumerable<T> source, Func<T, ProductBatch> batch) =>
        source
            .OrderBy(x => batch(x).ExpirationDate == null)
            .ThenBy(x => batch(x).ExpirationDate)
            .ThenBy(x => batch(x).Id);

    private async Task EnsureCustomerIsActiveAsync(int? customerId)
    {
        if (customerId is null)
        {
            return;
        }

        var customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId)
            ?? throw new BusinessException("O cliente informado não existe.");

        if (!customer.Active)
        {
            throw new BusinessException($"O cliente \"{customer.Name}\" está inativo.");
        }
    }

    /// <summary>Conversão para a linha da listagem, traduzida em SQL pelo EF.</summary>
    private static readonly Expression<Func<Sale, SaleDto>> ToDto = s => new SaleDto(
        s.Id,
        s.SaleDate,
        s.CustomerId,
        s.Customer == null ? null : s.Customer.Name,
        s.User.Name,
        s.Items.Count,
        s.Subtotal,
        s.Discount,
        s.Total,
        s.PaymentMethod,
        s.Status);
}
