# Fase 2: Fornecedores, Compras e Movimentações (guia de estudo)

Este guia acompanha o PR da Fase 2. Como no da Fase 1, você encontra o caminho de uma requisição, o papel de cada arquivo novo e as decisões tomadas. O assunto central desta fase é a **lógica centralizada de estoque**: vale ler a seção 2 com calma, porque as Fases 3 e 4 (lotes e vendas) vão se apoiar nela.

---

## 1. Como uma compra percorre o backend

### POST /api/purchases

```
Navegador (PurchaseFormPage)
  ↓ purchasesApi.create({ supplierId, invoiceNumber, purchaseDate, items: [{ productId, quantity, unitCost }] })
PurchasesController.Create
  ↓ purchaseService.CreateAsync(dto)
PurchaseService.CreateAsync
  ↓ 1. validator.ValidateAndThrowAsync(dto)     → itens não vazios, quantidade > 0, custo ≥ 0 com até 4 casas,
  ↓                                                produto repetido, data no futuro
  ↓ 2. EnsureSupplierIsActiveAsync              → fornecedor existe e está ativo
  ↓ 3. EnsureInvoiceIsNewAsync                  → mesma nota não pode entrar duas vezes para o mesmo fornecedor
  ↓ 4. carrega os produtos e confere            → todos existem e estão ativos
  ↓ 5. monta Purchase + PurchaseItems           → Total = Quantity x UnitCost (2 casas); TotalAmount = soma
  ↓ ── BeginTransaction ──────────────────────────────────────────────────────────────
  ↓ 6. SaveChanges                              → INSERT da compra e dos itens (gera o Id da compra)
  ↓ 7. para cada item:
  ↓      UpdateCostAsync                        → grava ProductPriceHistory e atualiza Product.CostPrice
  ↓      stockService.RegisterMovementAsync     → muda CurrentStock e grava StockMovement (PURCHASE, +qtd)
  ↓ 8. SaveChanges + Commit                     → tudo vira definitivo de uma vez
  ↓ ──────────────────────────────────────────────────────────────────────────────────
  ↓ 9. GetByIdAsync                             → devolve a compra completa
PurchasesController
  ↓ 201 Created + ApiResponse<PurchaseDetailsDto>
Navegador
  ↓ volta para /compras com "Compra #4 registrada. O estoque foi atualizado."
```

**E se der erro no meio?** Os passos 1 a 4 acontecem antes de gravar qualquer coisa. Se algo falhar entre o `BeginTransaction` e o `Commit`, a variável `transaction` é descartada pelo `await using` sem ter feito `Commit`, e o banco desfaz tudo (rollback). Não existe "compra gravada com metade do estoque atualizado". Há um teste para isso: `Create_WithInactiveProduct_ThrowsAndSavesNothing`.

### POST /api/stock/adjustments

```
StockController.Adjust
  ↓ stockService.AdjustAsync(dto)
StockService.AdjustAsync
  ↓ 1. validator                → tipo ADJUSTMENT ou LOSS; quantidade ≠ 0; LOSS sempre negativa; motivo obrigatório
  ↓ 2. BeginTransaction
  ↓ 3. RegisterMovementAsync    → o mesmo método que a compra usa
  ↓ 4. Commit
  ↓ 200 + ApiResponse<StockMovementDto>   (com estoque anterior e novo)
```

---

## 2. A lógica centralizada de estoque

A regra que você definiu: *nenhuma parte do sistema altera `Product.CurrentStock` diretamente; toda alteração passa por uma lógica centralizada que mantém produto, lote e movimentação consistentes na mesma transação.* Ela está garantida em quatro camadas:

| Camada | Onde | O que impede |
|---|---|---|
| Setter privado | `Product.CurrentStock { get; private set; }` | Ninguém escreve `product.CurrentStock = 10`. O compilador recusa. |
| Método único | `Product.ApplyStockChange(int)` (internal) | É a única forma de mudar o valor, e ele recusa estoque negativo. Só o `StockService` chama. |
| Serviço central | `StockService.RegisterMovementAsync` | Muda o estoque **e** grava a `StockMovement` juntos, com estoque anterior, novo, motivo e usuário. |
| Transação obrigatória | início do `RegisterMovementAsync` | Se não houver transação aberta, lança `InvalidOperationException`. Quem chamar sem transação descobre no primeiro teste. |

**Sinal da quantidade.** Cada tipo de movimentação tem um sinal fixo, conferido pelo `EnsureQuantitySign`:

| Tipo | Sinal | Quem usa |
|---|---|---|
| `PURCHASE` | + | compras (esta fase) |
| `ADJUSTMENT` | + ou − | correção de inventário (esta fase) |
| `LOSS` | − | perdas e quebras (esta fase) |
| `SALE` | − | vendas (Fase 4) |
| `EXPIRED` | − | baixa de lote vencido (Fase 3) |
| `RETURN` | + | cancelamento de venda (Fase 4) |

Assim, o extrato de um produto fecha sempre: a soma das quantidades é o estoque atual, e cada linha mostra `PreviousStock → NewStock`.

**Duas pessoas mexendo no mesmo produto ao mesmo tempo.** Imagine dois caixas registrando perdas da mesma cerveja no mesmo segundo. Os dois leem estoque 36, cada um calcula 35 e grava 35. Uma das perdas sumiu. Para evitar isso, `CurrentStock` é um **token de concorrência** (`IsConcurrencyToken()` em `ProductConfiguration`). O EF passa a gerar:

```sql
UPDATE Products SET CurrentStock = 35 WHERE Id = 1 AND CurrentStock = 36;
```

O segundo `UPDATE` não encontra mais `CurrentStock = 36`, o EF lança `DbUpdateConcurrencyException`, a transação é desfeita e a API responde **409** "Os dados foram alterados por outra operação ao mesmo tempo". Testei disparando 20 perdas simultâneas no MySQL: 4 passaram, 16 receberam 409, e o estoque final bateu exatamente com as movimentações gravadas.

**E na Fase 3?** O `RegisterMovementAsync` vai receber o lote (`BatchId`) e atualizar também `ProductBatch.QuantityAvailable`, no mesmo lugar. Compras, ajustes e vendas não vão precisar mudar a forma de chamar.

---

## 3. Backend: arquivos novos e alterados

### Entities
| Arquivo | Pontos importantes |
|---|---|
| `Enums/UserRole.cs`, `Enums/MovementType.cs` | Valores em maiúsculas, como no enunciado (`ADMIN`, `PURCHASE`). Gravados como texto no banco (`HasConversion<string>()`). |
| `User.cs` | Só o necessário para registrar quem fez cada operação. `PasswordHash` existe, mas o login é da Fase 5. |
| `Supplier.cs` | `Cnpj` gravado sem máscara e em maiúsculas. |
| `Purchase.cs`, `PurchaseItem.cs` | `PurchaseDate` é `DateOnly` (dia da nota, sem hora). `UnitCost` com 4 casas, `Total` com 2. Compras não têm edição nem exclusão. |
| `StockMovement.cs` | `Quantity` com sinal; `PreviousStock`/`NewStock`; `ReferenceType`/`ReferenceId` apontam o documento de origem (`"Purchase", 4`). `BatchId` fica nulo até a Fase 3. |
| `ProductPriceHistory.cs` | Uma linha por produto em cada compra. A tela completa de evolução de preços é da Fase 7, mas o histórico já começa a ser gravado agora. |
| `Product.cs` (alterado) | Ganhou `ApplyStockChange`. Nada mais mudou. |

### Data
| Arquivo | Pontos importantes |
|---|---|
| `Configurations/*Configuration.cs` | Restrict em todas as chaves estrangeiras, exceto compra → itens (Cascade, porque item não existe sem a compra). Índice único `(SupplierId, InvoiceNumber)`: a mesma nota pode existir em fornecedores diferentes. Índices em `StockMovements` para o extrato por produto e por data. |
| `ProductConfiguration.cs` (alterado) | Só a linha `IsConcurrencyToken()`. Não muda o esquema do banco. |
| `Seed/UserSeed.cs` | Admin com Id 1, `admin@adega.local`. **`PasswordHash` vazio**: nenhuma senha vai para o repositório. Na Fase 5 a senha inicial virá de User Secrets ou variável de ambiente. |
| `DateOnlyConverter.cs` | O provider oficial do MySQL grava `DateOnly`, mas quebra ao ler ("Unable to cast DateTime to DateOnly"). Descobri isso no teste contra o MySQL real (o SQLite dos testes não tem o problema). O conversor entrega `DateTime` ao provider e a coluna continua `date`. |
| `AdegaDbContext.cs` (alterado) | Novos `DbSet`s e o conversor de `DateOnly` nas convenções. |
| `Migrations/*_Phase2SuppliersPurchasesStock.cs` | Cria `Users` (com o admin), `Suppliers`, `Purchases`, `PurchaseItems`, `StockMovements`, `ProductPriceHistories`. |

### Common
| Arquivo | Pontos importantes |
|---|---|
| `Cnpj.cs` | Valida os dígitos verificadores. Aceita o **CNPJ alfanumérico**, emitido pela Receita desde julho de 2026: cada caractere vale seu código ASCII menos 48 (`'0'..'9'` = 0..9, `'A'..'Z'` = 17..42), e a mesma conta serve para os dois formatos. |
| `BusinessClock.cs` | "Hoje" e "o dia 01/10" no horário de Brasília. O banco guarda UTC, e às 22h de 01/10 em Brasília já é 02/10 em UTC. Sem isso, o filtro por data do extrato jogaria as movimentações da noite para o dia seguinte. |

### DTOs e Validators
- `CreatePurchaseDto` **não tem totais**: a API calcula. O frontend mostra uma prévia, mas o valor oficial é o do backend.
- `StockMovementRequest` é um pedido interno entre services; o usuário só envia `StockAdjustmentDto`.
- `PurchaseValidators` usa `RuleForEach(...).SetValidator(...)` para validar cada item. Os erros saem como `items[0].quantity`, e o formulário marca a linha certa.
- `PrecisionScale(12, 4)` no custo unitário: sem isso, um custo de 5 casas seria arredondado pelo banco sem avisar.

### Services
| Arquivo | Pontos importantes |
|---|---|
| `ICurrentUserService` / `CurrentUserService` | Devolve o admin semeado. Na Fase 5 passa a ler o usuário do token JWT, e nenhum outro service muda. |
| `StockService` | Seção 2. `GetMovementsAsync` usa um `Expression` (`ToDto`) para o EF transformar a conversão em SQL com JOIN, trazendo só as colunas usadas. |
| `PurchaseService` | Seção 1. **Custo do produto = custo da compra mais recente.** Se alguém lançar hoje uma nota antiga, ela entra no histórico, mas não substitui um custo mais novo (teste `Create_BackdatedPurchase_KeepsNewerCostButRecordsHistory`). Arredondamento comercial (`MidpointRounding.AwayFromZero`), o mesmo da calculadora. |
| `SupplierService` | CRUD e resumo. As somas do resumo são feitas em memória: é simples, o volume por fornecedor é pequeno, e o SQLite dos testes não sabe somar `decimal`. |
| `CostHistoryService` | Ordena por data da compra e calcula a variação de cada registro em relação ao anterior e do primeiro ao último. |

### Controllers
- `SuppliersController`: CRUD, `/{id}/purchases` (reaproveita a listagem de compras) e `/{id}/summary`.
- `PurchasesController`: só GET e POST. Compras são documentos.
- `StockController`: `GET /api/stock-movements` e `POST /api/stock/adjustments`.
- `CostHistoryController`: `GET /api/products/{id}/cost-history`. Ficou num controller próprio para não mexer no `ProductsController` da Fase 1.

### Middleware/GlobalExceptionHandler.cs (alterado)
- `DbUpdateConcurrencyException` → 409.
- Nomes de campo aninhados convertidos parte a parte: `Items[0].UnitCost` → `items[0].unitCost`.

---

## 4. Testes

46 testes novos, 66 no total. Foram escritos antes do código (TDD) e falharam até ele existir.

| Arquivo | O que cobre |
|---|---|
| `Infrastructure/TestData.cs` | Atalhos para criar produto e fornecedor direto no banco, e o `FakeCurrentUser`. |
| `Common/CnpjTests.cs` | CNPJ com e sem máscara, alfanumérico, minúsculas, dígito errado, todos iguais, tamanho errado. |
| `Services/StockServiceTests.cs` | Entrada grava movimentação completa; **sem transação lança erro**; saída maior que o estoque é recusada e o estoque não muda; sinal errado por tipo; perda e correção de inventário; validação do ajuste; filtros e ordem do extrato. |
| `Services/PurchaseServiceTests.cs` | Totais (12 x 3,8975 = 46,77); estoque e movimentações; custo e histórico; **produto inativo não grava nada**; fornecedor inativo; produto repetido; nota repetida; data futura; compra retroativa; filtro por fornecedor. |
| `Services/SupplierServiceTests.cs` | CNPJ normalizado, alfanumérico, inválido, repetido, opcional; soft delete; resumo com e sem compras. |
| `Services/CostHistoryServiceTests.cs` | 3,90 → 4,10 (+5,13%) → 4,35 (+6,10%), total +11,54%; sem compras; produto inexistente. |

---

## 5. Frontend

| Arquivo | Para que serve | Ponto importante |
|---|---|---|
| `components/ProductPicker.tsx` | Busca de produto com sugestões | Reaproveitado na compra, no ajuste e no filtro do extrato (e no PDV da Fase 4). Com leitor de código de barras, o leitor "digita" o código e aperta Enter, e o primeiro resultado é escolhido. |
| `pages/suppliers/*` | Lista, formulário e resumo do fornecedor | O CNPJ vai como foi digitado; o backend valida e normaliza. A lista mostra com máscara. |
| `pages/purchases/PurchasesPage.tsx` | Lista com filtro por fornecedor e período | Recebe a mensagem de sucesso da tela de nova compra por `location.state`. |
| `pages/purchases/PurchaseFormPage.tsx` | Nova compra | Página inteira (não modal), porque uma nota pode ter muitos itens. O custo sugerido é o custo atual do produto. |
| `pages/purchases/PurchaseDetailsModal.tsx` | Detalhes da compra | Custo unitário com até 4 casas. |
| `pages/stock/StockPage.tsx` | Extrato de movimentações | Entradas em verde, saídas em vermelho, `anterior → novo`. |
| `pages/stock/StockAdjustmentModal.tsx` | Ajuste de estoque | O usuário escolhe "Perda", "Correção: saída" ou "Correção: entrada" e digita uma quantidade positiva; o sinal é definido pela tela. Mostra o estoque resultante antes de salvar. |
| `pages/products/CostHistoryModal.tsx` | Botão "Custos" na lista de produtos | Única alteração na tela da Fase 1: um botão a mais. |
| `utils/format.ts` | `formatDate`, `formatDateTime`, `formatCnpj`, `todayIso` | `formatDate` não usa `new Date()` para datas sem hora: `new Date("2026-10-01")` é meia-noite UTC, que no Brasil vira 30/09. |

---

## 6. Decisões desta fase

1. **Transação obrigatória no `StockService`**, verificada em código, em vez de confiar que todo mundo vai lembrar.
2. **Token de concorrência no estoque** (409 em vez de perder uma movimentação).
3. **Custo do produto = última compra**, sem custo médio. O custo real por lote, usado no lucro das vendas, chega com os lotes na Fase 3.
4. **Compra retroativa não sobrescreve um custo mais novo.**
5. **Mesmo produto duas vezes na compra é recusado**: some as quantidades numa linha só. Na Fase 3 isso muda, porque o mesmo produto pode vir em dois lotes diferentes.
6. **Perda sempre negativa e ajuste com motivo obrigatório**, para o extrato explicar cada diferença.
7. **Admin semeado sem senha** no repositório.
8. **CNPJ opcional** (feira, produtor rural), mas válido e único quando informado.

## 7. Como testar na sua máquina

```bash
git pull
dotnet ef database update --project backend/src/AdegaSystem.Api
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http
# em outro terminal
cd frontend && npm run dev
```

Roteiro sugerido:
1. Cadastre um fornecedor em **Fornecedores** (teste um CNPJ errado para ver a validação).
2. Em **Compras → Nova compra**, adicione dois produtos e registre.
3. Veja em **Estoque** as entradas da compra e registre uma perda.
4. Em **Produtos**, clique em **Custos** para ver o histórico.
