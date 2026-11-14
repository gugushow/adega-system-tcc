# Fase 3: Lotes, Validade e Estoque por Lote (guia de estudo)

Este guia acompanha o PR da Fase 3. A Fase 2 criou o `StockService` como único caminho para mexer no estoque. Esta fase coloca o **lote** no meio desse caminho. Daqui em diante, toda unidade no estoque pertence a um lote, e toda entrada ou saída diz de qual lote foi. É essa base que vai permitir o FEFO e o cancelamento de venda na Fase 4.

---

## 1. A regra nova: estoque = soma dos lotes

```
Heineken 350ml                         CurrentStock = 80
 ├─ Lote A  · validade 10/11/2026      QuantityAvailable = 30
 └─ Lote B  · validade 20/01/2027      QuantityAvailable = 50
```

`Product.CurrentStock` continua no banco (as listagens e os filtros de estoque baixo ficam rápidos), mas agora ele é sempre a soma de `QuantityAvailable` dos lotes do produto. Quem garante isso é o `StockService`: ele altera o lote e o produto **pela mesma quantidade, no mesmo `SaveChanges`**, e grava a movimentação junto.

| Camada | Produto | Lote |
|---|---|---|
| Setter privado | `CurrentStock { get; private set; }` | `QuantityAvailable { get; private set; }` |
| Método único (internal) | `ApplyStockChange` | `ApplyQuantityChange` |
| Nunca negativo | sim | sim |
| Token de concorrência | sim | sim |

Os testes conferem a regra com `TestData.AssertStockMatchesBatchesAsync` depois de compras, ajustes e baixas.

---

## 2. O StockService com lotes

Antes havia um método só. Agora são dois, e todos os outros services passam por eles:

| Método | Quando | O que faz |
|---|---|---|
| `ReceiveBatchAsync(ReceiveBatchRequest)` | compras | Cria um **lote novo**, soma no estoque do produto e grava a movimentação `PURCHASE` com o `BatchId`. |
| `RegisterMovementAsync(StockMovementRequest)` | ajustes, perdas, vencidos (e vendas e devoluções na Fase 4) | Entrada ou saída num **lote que já existe**. O produto vem do próprio lote. |

Os dois exigem uma transação aberta e terminam no mesmo método privado, `ApplyAsync`:

```
ApplyAsync(product, batch, tipo, quantidade)
  ↓ previousStock = product.CurrentStock
  ↓ batch.ApplyQuantityChange(quantidade)     ← saldo do lote
  ↓ product.ApplyStockChange(quantidade)      ← estoque do produto
  ↓ new StockMovement { Batch = batch, PreviousStock, NewStock, ... }
  ↓ SaveChanges                               ← os três juntos, dentro da transação de quem chamou
```

**Por que `Batch = batch` e não `BatchId = batch.Id`?** Na compra, o lote acabou de ser criado e ainda não tem Id (o banco só gera no `INSERT`). Ligando pela navegação, o EF grava primeiro o lote e depois usa o Id gerado na movimentação.

**A saída é sempre de um lote específico.** Se o lote A tem 5 unidades e o B tem 50, uma perda de 6 no lote A é recusada ("Saldo insuficiente no lote A"), mesmo o produto tendo 55. Isso mantém cada lote honesto, e é exatamente o que a venda com FEFO vai precisar.

**`PURCHASE` não entra no `RegisterMovementAsync`.** Toda compra cria um lote novo; somar uma compra num lote antigo misturaria validades e custos diferentes.

---

## 3. Como uma compra percorre o backend agora

```
POST /api/purchases  { items: [ { productId, quantity, unitCost, batchNumber, manufacturingDate, expirationDate } ] }
PurchaseService.CreateAsync
  ↓ validator     → mesmo produto pode repetir, mas não o mesmo lote; validade depois da fabricação
  ↓ fornecedor, nota, produtos ativos (como na Fase 2)
  ↓ ── BeginTransaction ──
  ↓ SaveChanges da compra                   → gera o Id
  ↓ para cada item:
  ↓    UpdateCostAsync                      → custo e histórico (como na Fase 2)
  ↓    stockService.ReceiveBatchAsync       → cria o lote, soma o estoque, grava PURCHASE
  ↓    item.BatchId = batch.Id              → o item da compra aponta para o lote que gerou
  ↓ SaveChanges + Commit
```

- **Número do lote em branco** vira `COMPRA-{id da compra}`. Muita nota não traz o lote, e assim todo lote tem um nome para aparecer nas telas.
- **Validade em branco** significa "não vence" (gelo, por exemplo). Esses lotes nunca aparecem como vencidos e ficam no fim da ordem por validade.
- **O mesmo produto em duas linhas** agora é permitido, um lote por linha (a Heineken com validades 10/11 e 20/01 do enunciado). Repetir o mesmo número de lote do mesmo produto é recusado.

---

## 4. Validade e baixa de vencidos

"Hoje" é o dia no horário de Brasília (`BusinessClock.Today`, da Fase 2).

| Situação | Regra | Endpoint |
|---|---|---|
| Vencido | `ExpirationDate < hoje` e saldo > 0 | `GET /api/batches/expired` |
| Vencendo | `hoje ≤ ExpirationDate ≤ hoje + dias` e saldo > 0 | `GET /api/batches/expiring?days=7` (30, 60...) |
| Sem validade | `ExpirationDate` nulo | nunca entra nos dois filtros |

**Um lote que vence hoje não está vencido.** Ele ainda pode ser vendido hoje e aparece em "vencendo". Vencido é só a partir de amanhã. Isso está nos testes.

**Baixa de lote vencido** (`POST /api/batches/{id}/write-off`):

```
BatchService.WriteOffAsync
  ↓ lote existe?                     → 404
  ↓ está vencido?                    → senão: "Só é possível dar baixa em lotes vencidos..."
  ↓ tem saldo?                       → senão: "Este lote não tem mais unidades..."
  ↓ BeginTransaction
  ↓ stockService.RegisterMovementAsync(EXPIRED, -saldo, "Lote A vencido em 30/09/2026")
  ↓ Commit
```

Por que não deixar dar baixa num lote que ainda vale? Porque aí não é vencimento, é perda (`LOSS`), e os relatórios da Fase 6 precisam separar uma coisa da outra.

---

## 5. A migration e o estoque que já existia

Ao rodar a migration, os produtos que já tinham estoque (das compras da Fase 2) não tinham lote nenhum, e a regra "estoque = soma dos lotes" quebraria logo no primeiro dia. Por isso a migration `Phase3ProductBatches` tem um trecho escrito à mão:

```sql
INSERT INTO ProductBatches (ProductId, PurchaseId, BatchNumber, QuantityReceived, QuantityAvailable, ..., UnitCost, CreatedAt)
SELECT Id, NULL, 'SALDO-INICIAL', CurrentStock, CurrentStock, ..., CostPrice, UTC_TIMESTAMP(6)
FROM Products WHERE CurrentStock > 0;
```

Cada produto com estoque ganha um lote `SALDO-INICIAL`, sem validade e com o custo atual. Testei isso no MySQL com os dados da Fase 2: os dois produtos ganharam o lote com o saldo exato. Se quiser, depois você corrige esse saldo pela tela de Estoque ou simplesmente recria o banco de desenvolvimento.

As movimentações e os itens de compra antigos continuam com `BatchId` nulo, porque foram gravados antes de os lotes existirem.

---

## 6. Backend: arquivos novos e alterados

| Arquivo | Pontos importantes |
|---|---|
| `Entities/ProductBatch.cs` | `QuantityAvailable` com setter privado e `ApplyQuantityChange`. `PurchaseId` nulo só no lote de saldo inicial. |
| `Entities/StockMovement.cs`, `PurchaseItem.cs` | Ganharam a navegação `Batch`. O `PurchaseItem` aponta para o lote que gerou, em vez de copiar número e datas: o lote é o único lugar onde esses dados ficam. |
| `Data/Configurations/ProductBatchConfiguration.cs` | Índices em `ExpirationDate` e `(ProductId, ExpirationDate)` (consultas de validade e o FEFO da Fase 4). Token de concorrência no saldo. Restrict nas chaves estrangeiras. |
| `Data/Migrations/*_Phase3ProductBatches.cs` | Tabela `ProductBatches`, `PurchaseItems.BatchId`, chave estrangeira de `StockMovements.BatchId` (prevista na Fase 2) e o lote de saldo inicial. |
| `DTOs/Stock/StockDtos.cs` | `ReceiveBatchRequest` (novo), `StockMovementRequest` e `StockAdjustmentDto` agora recebem `BatchId` em vez de `ProductId`. A movimentação devolve `batchNumber`. |
| `DTOs/Batches/BatchDtos.cs` | `BatchDto` traz `DaysToExpire` e `IsExpired` já calculados. `BatchQuery` limita `Days` entre 1 e 365. |
| `DTOs/Purchases/PurchaseDtos.cs` | Item com `BatchNumber`, `ManufacturingDate` e `ExpirationDate` opcionais (parâmetros com valor padrão no record). |
| `Validators/PurchaseValidators.cs` | Repetição verificada por (produto, lote), ignorando maiúsculas e espaços. Validade depois da fabricação. |
| `Services/StockService.cs` | Seção 2. |
| `Services/BatchService.cs` | Seções 4 e 5. A ordenação `OrderBy(b => b.ExpirationDate == null).ThenBy(b => b.ExpirationDate)` joga os lotes sem validade para o fim (para quem tem data, `== null` vale `false`, que vem antes de `true`). |
| `Services/PurchaseService.cs` | Seção 3. |
| `Controllers/BatchesController.cs` | `/expired` e `/expiring` reaproveitam o `GetAll` com o filtro preenchido, igual a `/low-stock` dos produtos. |

---

## 7. Testes

81 no total (15 novos, e os de estoque e compras reescritos para lotes). Escritos antes do código.

| Arquivo | O que cobre |
|---|---|
| `StockServiceTests` | Entrada cria lote e movimentação com `BatchId`; sem transação lança erro (nos dois métodos); saída diminui lote e produto juntos; **saída maior que o lote é recusada mesmo com estoque em outro lote**; sinais por tipo; `PURCHASE` só pela entrada de lote; ajuste e perda no lote escolhido; lote inexistente; extrato com o número do lote. |
| `BatchServiceTests` | Ordem por validade com "sem validade" no fim; esconder lotes esgotados; vencidos (ontem sim, hoje não, sem validade não); vencendo em 7 dias (de hoje até o 7º dia); baixa de vencido zera o lote e grava `EXPIRED`; baixa de lote não vencido, vazio ou inexistente é recusada. |
| `PurchaseServiceTests` | Mesmo produto em dois lotes; número de lote gerado; mesmo lote duas vezes recusado; validade antes da fabricação recusada; e todos os testes da Fase 2. |

---

## 8. Frontend

| Arquivo | Para que serve | Ponto importante |
|---|---|---|
| `pages/batches/BatchesPage.tsx` | Tela de Lotes | Abas "Com saldo", "Vencidos", "Vencem em 7 / 30 / 60 dias" e "Todos". Botão "Dar baixa" só em lote vencido com saldo, com confirmação. |
| `components/ExpirationBadge.tsx` | Selo de validade | Vermelho vencido ou até 7 dias, amarelo até 30, cinza sem validade. Reaproveitável no PDV da Fase 4. |
| `pages/purchases/PurchaseFormPage.tsx` | Nova compra | Cada linha tem lote, fabricação e validade. O mesmo produto pode ser adicionado de novo, por isso cada linha ganhou uma `key` própria da tela. |
| `pages/purchases/PurchaseDetailsModal.tsx` | Detalhes | Mostra lote e validade de cada item. |
| `pages/stock/StockAdjustmentModal.tsx` | Ajuste | Depois do produto, escolhe o lote. Em saídas, só aparecem lotes com saldo. A prévia mostra o saldo do lote depois do ajuste. |
| `pages/stock/StockPage.tsx` | Extrato | Nova coluna Lote. |

---

## 9. Decisões desta fase

1. **Um lote por item de compra**, nunca somando num lote existente.
2. **Toda saída escolhe o lote.** Na Fase 4 quem escolhe é o FEFO; nos ajustes, a pessoa.
3. **Validade opcional** para produtos que não vencem, com esses lotes sempre no fim da fila.
4. **Vencido = validade antes de hoje** (no horário de Brasília). O que vence hoje ainda vale hoje.
5. **Baixa só de lote vencido**; outras saídas são perda ou ajuste.
6. **Lote `SALDO-INICIAL` criado pela migration** para o estoque que já existia.
7. **Número do lote gerado** (`COMPRA-15`) quando a nota não traz.
8. **Estoque mínimo:** os filtros de estoque baixo e sem estoque já existem desde a Fase 1 e continuam valendo. Os cards de alerta ficam para o dashboard da Fase 6.

## 10. Como testar na sua máquina

```bash
git pull
dotnet ef database update --project backend/src/AdegaSystem.Api
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http
# em outro terminal
cd frontend && npm run dev
```

Roteiro sugerido:
1. Abra **Lotes**: os produtos que já tinham estoque aparecem com o lote `SALDO-INICIAL`.
2. Em **Compras → Nova compra**, adicione a mesma cerveja duas vezes, com validades diferentes.
3. Registre outra compra com data passada e validade já vencida. Ela aparece em **Lotes → Vencidos**; clique em **Dar baixa**.
4. Em **Estoque → Ajustar estoque**, escolha o produto e o lote e registre uma perda.
