# Fase 4: Clientes, Vendas, FEFO e Cancelamento (guia de estudo)

Este guia acompanha o PR da Fase 4. As fases anteriores prepararam o terreno: o `StockService` é o único caminho para mexer no estoque (Fase 2), e toda unidade pertence a um lote com validade (Fase 3). Agora chega o motivo de tudo isso existir: **vender**. A venda escolhe os lotes pelo FEFO, pede ao `StockService` para dar a baixa, guarda de onde saiu cada unidade e calcula o lucro com o custo real dos lotes. O cancelamento faz o caminho inverso, lote por lote.

Autenticação e permissões continuam fora (Fase 5): por enquanto tudo é registrado em nome do admin semeado, e qualquer um pode cancelar uma venda.

---

## 1. As entidades novas

```
Customer (opcional) 1 ──── N Sale 1 ──── N SaleItem 1 ──── N SaleItemBatch N ──── 1 ProductBatch
                                                 │
                                                 N ──── 1 Product
```

| Entidade | Para que serve | Campos que merecem atenção |
|---|---|---|
| `Customer` | Cliente da adega | `Cpf` só com os 11 dígitos, único quando preenchido; `BirthDate` é `DateOnly`; `Active` (soft delete) |
| `Sale` | O documento da venda | `Subtotal`, `Discount`, `Total`, `TotalCost`, `Profit`; `Status`; `CanceledAt`, `CanceledByUserId`, `CancelReason` |
| `SaleItem` | Uma linha da venda | `UnitPrice` copiado do cadastro; `CostPrice` = custo médio dos lotes usados; `Discount` da linha |
| `SaleItemBatch` | Quanto saiu de cada lote | `BatchId`, `Quantity`, `UnitCost` copiado do lote |

E dois enums, gravados como texto no banco: `PaymentMethod` (`PIX`, `CASH`, `CREDIT_CARD`, `DEBIT_CARD`) e `SaleStatus` (`COMPLETED`, `CANCELED`).

**Por que `SaleItemBatch`?** O enunciado não pede, mas sem ela o cancelamento não teria como saber para quais lotes devolver. Uma venda de 5 Heineken pode ter tirado 3 do lote A e 2 do lote B. Guardar só "5 Heineken" perderia essa informação.

**Por que copiar preço e custo para a venda?** Porque eles mudam. Se a Heineken passar de R$ 6,50 para R$ 7,00 amanhã, a venda de hoje continua valendo R$ 6,50, e o lucro dela não muda. Venda é um documento: congela os valores do momento.

---

## 2. O fluxo de uma venda (POST /api/sales)

```
SalesController.Create
  ↓ CreateSaleDto validado (itens > 0, quantidade > 0, desconto ≥ 0, um produto por linha)
SaleService.CreateAsync
  ↓ cliente existe e está ativo? (se informado)
  ↓ BEGIN TRANSACTION
  ↓ carrega os produtos e os lotes com saldo desses produtos (rastreados)
  ↓ para cada item:
  │    produto existe e está ativo?
  │    preço = SalePrice do cadastro (o frontend não manda preço)
  │    desconto da linha ≤ valor da linha?
  │    FEFO: lotes não vencidos, por validade → soma < pedido? BusinessException → ROLLBACK
  │    monta o SaleItem e um SaleItemBatch por lote usado
  ↓ Subtotal, Discount (≤ Subtotal), Total, TotalCost, Profit
  ↓ SaveChanges (gera o Id da venda)
  ↓ para cada SaleItemBatch: StockService.RegisterMovementAsync(lote, SALE, −qtd, "Venda #id")
  │    → lote, produto e StockMovement mudam juntos (a regra central da Fase 2)
  ↓ COMMIT
  ↓ 201 Created + SaleDetailsDto
```

Repare na ordem: **tudo o que pode dar errado é conferido antes de gravar qualquer coisa** (produto inativo, desconto, falta de estoque). Mesmo assim, a transação garante o resto: se algo falhar depois do primeiro `SaveChanges`, o `await using var transaction` desfaz tudo ao sair do método sem `Commit`.

Os produtos e lotes são carregados **rastreados** (sem `AsNoTracking`). Assim, quando o `StockService` faz `FindAsync` no mesmo lote, recebe a mesma instância que o `SaleService` já tem em memória, e os dois enxergam o mesmo saldo.

---

## 3. FEFO: First Expired, First Out

O FEFO está no método privado `PickBatches` do `SaleService`:

```csharp
var sellable = OrderFefo(batches.Where(b => b.ExpirationDate == null || b.ExpirationDate >= today), b => b).ToList();
```

1. **Filtra:** só lotes com saldo e **não vencidos**. Vence hoje ainda pode ser vendido hoje (a mesma regra da tela de Lotes).
2. **Ordena:** validade mais próxima primeiro; lotes sem validade (gelo) por último; empate pelo Id, ou seja, o lote mais antigo primeiro.
3. **Confere:** se a soma dos lotes vendáveis for menor que o pedido, a venda é recusada. A mensagem diz quanto há disponível e, se for o caso, quantas unidades estão vencidas:
   > Estoque insuficiente para "Vinho Tinto". Disponível para venda: 2, solicitado: 3. Há 10 unidades vencidas, que não podem ser vendidas.
4. **Consome:** vai tirando de cada lote o que ele tem, até completar a quantidade.

Exemplo, venda de 5 Heineken:

| Lote | Validade | Saldo antes | Usado | Saldo depois |
|---|---|---|---|---|
| X | 01/09/2026 (vencido) | 4 | 0 | 4 |
| A | 10/11/2026 | 3 | **3** | 0 |
| B | 20/01/2027 | 50 | **2** | 48 |
| Gelo-sem-validade | — | 10 | 0 | 10 |

Isso gera **duas** movimentações `SALE` (uma por lote), cada uma com `PreviousStock` e `NewStock` do produto. O extrato da tela de Estoque mostra exatamente de onde saiu cada unidade.

No exemplo, por que o PDV mostra "estoque 67" se só 63 unidades podem ser vendidas? Porque `CurrentStock` é a soma de todos os lotes, inclusive os vencidos (que ainda estão fisicamente na prateleira até a baixa). Quem decide o que pode ser vendido é a API. Para o número bater, dê baixa nos vencidos pela tela de Lotes.

---

## 4. As contas da venda

Todas feitas no backend. O frontend só mostra uma prévia.

```
Linha:   Total do item = Quantidade × Preço − Desconto da linha
Venda:   Subtotal      = soma dos totais das linhas
         Total         = Subtotal − Desconto da venda
Custo:   TotalCost     = soma de (quantidade × custo do lote) de cada SaleItemBatch
Lucro:   Profit        = Total − TotalCost
```

Exemplo do teste `Create_CalculatesTotalsAndProfitFromTheBatchCosts`: 3 unidades a R$ 10,00, com desconto de R$ 1,00 na linha e R$ 2,00 na venda. Duas saíram de um lote a R$ 4,00 e uma de um lote a R$ 5,00.

| Conta | Valor |
|---|---|
| Total do item | 3 × 10,00 − 1,00 = **29,00** |
| Custo médio do item (`CostPrice`) | (2 × 4,00 + 1 × 5,00) / 3 = **4,3333** |
| Total da venda | 29,00 − 2,00 = **27,00** |
| Custo (`TotalCost`) | 8,00 + 5,00 = **13,00** |
| Lucro | 27,00 − 13,00 = **14,00** |

Dois detalhes:
- `TotalCost` usa o custo de cada lote, não o custo médio arredondado (4,3333 × 3 = 12,9999). Assim o centavo não some.
- Os dois descontos existem porque o enunciado tem `Discount` em `Sale` e em `SaleItem`. O PDV usa só o desconto da venda (o mais comum no balcão); a API aceita os dois.

---

## 5. Cancelamento (POST /api/sales/{id}/cancel)

```
SalesController.Cancel
  ↓ CancelSaleDto validado (motivo obrigatório, até 255 caracteres)
SaleService.CancelAsync
  ↓ BEGIN TRANSACTION
  ↓ carrega a venda com Items e Batches (os SaleItemBatch)
  ↓ já cancelada? BusinessException
  ↓ para cada SaleItemBatch: StockService.RegisterMovementAsync(mesmo lote, RETURN, +qtd, "Cancelamento da venda #id")
  ↓ Status = CANCELED, CanceledAt, CanceledByUserId, CancelReason
  ↓ SaveChanges + COMMIT
```

- As unidades voltam **para o mesmo lote de onde saíram**, com a validade e o custo originais. Se esse lote venceu depois da venda, elas voltam mesmo assim e aparecem em Lotes → Vencidos, prontas para a baixa.
- A venda **não é apagada nem editada** nos valores: só muda o status e ganha os dados do cancelamento. O histórico fica completo.
- Venda cancelada não conta no resumo do cliente e, na Fase 6, não vai entrar no faturamento (o índice `(Status, SaleDate)` já existe para isso).
- Na Fase 5, este endpoint fica restrito ao perfil ADMIN.

---

## 6. Concorrência: dois caixas ao mesmo tempo

Os tokens de concorrência criados nas Fases 2 e 3 (`Product.CurrentStock` e `ProductBatch.QuantityAvailable`) são o que protege a venda. Cenário: sobrou 1 unidade e dois caixas vendem ao mesmo tempo.

1. Os dois leem o lote com saldo 1.
2. O caixa A grava primeiro: `UPDATE ... SET QuantityAvailable = 0 WHERE Id = 7 AND QuantityAvailable = 1`. Uma linha alterada, tudo certo.
3. O caixa B tenta o mesmo `UPDATE`, mas no banco o saldo já é 0. Nenhuma linha é alterada, o EF lança `DbUpdateConcurrencyException`, a transação desfaz a venda B inteira e o `GlobalExceptionHandler` responde **409** ("Os dados foram alterados por outra operação ao mesmo tempo...").

Nesta fase, `Sale.Status` também virou token de concorrência. Se duas pessoas cancelarem a mesma venda ao mesmo tempo, o segundo `UPDATE ... WHERE Status = 'COMPLETED'` não encontra a linha, e tudo o que o segundo cancelamento fez (inclusive as devoluções ao estoque) é desfeito. Sem isso, o estoque poderia voltar duas vezes.

---

## 7. Clientes

CRUD no mesmo formato dos fornecedores: `CustomerService` + validators + soft delete.

- **CPF:** opcional; quando informado, precisa ter os dígitos verificadores corretos (`Common/Cpf.cs`, a mesma ideia do `Cnpj.cs`). Aceita com ou sem máscara e grava só os 11 dígitos, para o índice único funcionar. CPFs com todos os dígitos iguais (111.111.111-11) são recusados.
- **Cadastro rápido:** só o nome é obrigatório, para o vendedor cadastrar alguém no PDV sem travar a fila.
- **Resumo** (`GET /api/customers/{id}/summary`): número de compras, total gasto, última compra e ticket médio (total ÷ compras), **só com vendas concluídas**.
- **Busca:** por nome, CPF (com ou sem máscara) ou telefone.

---

## 8. Backend: arquivos novos e alterados

| Arquivo | O que é |
|---|---|
| `Entities/Customer.cs`, `Sale.cs`, `SaleItem.cs`, `SaleItemBatch.cs` | As entidades da seção 1 |
| `Entities/Enums/PaymentMethod.cs`, `SaleStatus.cs` | Enums gravados como texto |
| `Data/Configurations/Customer…`, `Sale…`, `SaleItem…`, `SaleItemBatchConfiguration.cs` | Fluent API: tamanhos, precisão dos valores, índices, `Restrict` nas chaves e `Cascade` só do documento para os itens |
| `Data/Migrations/…_Phase4CustomersSales.cs` | Cria as quatro tabelas (só gerada pelo EF, sem SQL à mão) |
| `Common/Cpf.cs` | Validação de CPF |
| `Common/Money.cs` | "R$ 1.234,50" nas mensagens de erro, sem depender da cultura do servidor |
| `DTOs/Customers/CustomerDtos.cs`, `DTOs/Sales/SaleDtos.cs` | Contratos da API |
| `Validators/CustomerValidators.cs`, `SaleValidators.cs` | Regras que dá para conferir só olhando o pedido |
| `Services/CustomerService.cs` | CRUD, busca e resumo |
| `Services/SaleService.cs` | Venda com FEFO, cancelamento, listagem e detalhe |
| `Controllers/CustomersController.cs`, `SalesController.cs` | Endpoints (vendas não têm PUT nem DELETE) |
| `Mappings/CustomerMappings.cs` | Entidade → DTO |
| `Program.cs` | Registra `ICustomerService` e `ISaleService` |

O `StockService` **não mudou**: só os comentários. A venda e o cancelamento usam o mesmo `RegisterMovementAsync` que os ajustes e a baixa de vencidos já usavam. É a prova de que a centralização da Fase 2 valeu a pena.

---

## 9. Testes

34 testes novos (115 no total), escritos antes do código (TDD). Os principais, que são exatamente os da lista do enunciado:

| Regra | Teste |
|---|---|
| Venda com estoque suficiente | `Create_WithEnoughStock_SavesSaleAndReducesStock` |
| Venda sem estoque suficiente (rollback) | `Create_WithoutEnoughStock_ThrowsAndChangesNothing` |
| Venda usando vários lotes | `Create_SpansSeveralBatchesWhenOneIsNotEnough` |
| Prioridade para o lote que vence primeiro | `Create_UsesTheBatchThatExpiresFirst`, `Create_UsesBatchesWithoutExpiryLast` |
| Produto vencido não pode ser vendido | `Create_NeverSellsFromExpiredBatches`, `Create_BatchExpiringToday_CanStillBeSold` |
| Cálculo de lucro | `Create_CalculatesTotalsAndProfitFromTheBatchCosts` |
| Cancelamento de venda | `Cancel_ReturnsEachUnitToTheBatchItCameFrom`, `Cancel_TwiceThrows` |
| Resumo do cliente sem canceladas | `GetSummary_CountsOnlyCompletedSales` |

Depois de vendas e cancelamentos, os testes conferem `TestData.AssertStockMatchesBatchesAsync`: o estoque do produto continua sendo a soma dos lotes.

---

## 10. Frontend

| Tela | Rota | O que faz |
|---|---|---|
| PDV | `/vendas` | Produtos à esquerda (busca por nome ou código de barras; Enter com o leitor adiciona direto), carrinho à direita com quantidade, remover, cliente, forma de pagamento, desconto, subtotal e total. |
| Histórico de vendas | `/vendas/historico` | Filtros por período, situação e pagamento. "Detalhes" mostra os lotes usados, custo e lucro, e tem o botão **Cancelar venda** (com motivo). |
| Clientes | `/clientes` | Lista, cadastro, edição, desativação e histórico (resumo + últimas vendas). |

Arquivos: `pages/sales/PosPage.tsx`, `SalesPage.tsx`, `SaleDetailsModal.tsx`, `SaleStatusBadge.tsx`; `pages/customers/CustomersPage.tsx`, `CustomerFormModal.tsx`, `CustomerSummaryModal.tsx`; `components/CustomerPicker.tsx`; `api/sales.ts`, `api/customers.ts`; `types/sale.ts`, `types/customer.ts`.

O PDV não deixa a quantidade passar do estoque mostrado, mas isso é só conforto: a regra de verdade está na API (que também recusa unidades vencidas). Os totais da tela são uma prévia; a venda gravada usa os valores calculados no backend.

---

## 11. Decisões desta fase

1. **O preço vem do cadastro**, nunca do frontend. Ninguém consegue vender por R$ 0,01 mexendo na requisição.
2. **Uma linha por produto na venda.** No PDV, adicionar de novo aumenta a quantidade. Isso deixa o FEFO simples (um produto, uma escolha de lotes).
3. **Lote vencido nunca é vendido**, mas o que vence hoje ainda pode ser vendido hoje.
4. **Uma movimentação `SALE` por lote usado**, para o extrato mostrar de onde saiu cada unidade.
5. **Lucro com o custo real de cada lote**, não com o `CostPrice` atual do produto.
6. **Cancelamento devolve ao lote de origem** e nunca apaga a venda.
7. **`Sale.Status` como token de concorrência**, para um cancelamento duplo não devolver o estoque duas vezes.
8. **Cliente opcional, cadastro rápido só com o nome**, e resumo só com vendas concluídas.
9. **Combos ficam para a Fase 7.** `SaleItem` ainda não tem `ComboId`; ele entra quando os combos existirem, para não criar campo sem uso.

## 12. Como testar na sua máquina

```bash
git pull
dotnet ef database update --project backend/src/AdegaSystem.Api
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http
# em outro terminal
cd frontend && npm run dev
```

O banco continua sendo o container `adega-mysql` na porta 3307.

Roteiro sugerido:
1. Em **Clientes**, cadastre um cliente (teste um CPF inválido para ver a mensagem).
2. No **PDV** (menu Vendas), adicione uma cerveja que tenha dois lotes e peça mais do que o primeiro lote tem. Escolha o cliente, a forma de pagamento e um desconto, e finalize.
3. Clique em **Ver detalhes**: confira os lotes usados, o custo e o lucro.
4. Em **Estoque**, veja as movimentações `SALE` (uma por lote) e, em **Lotes**, os saldos novos.
5. Em **Histórico de vendas**, abra a venda e cancele com um motivo. Volte a **Lotes**: as unidades voltaram para os mesmos lotes.
6. Em **Clientes → Histórico**, confira que a venda cancelada não conta.
7. Para ver o FEFO recusando vencidos: registre uma compra com validade passada e tente vender mais do que os lotes válidos têm.
