# Fase 7: Combos, Fidelidade, Auditoria e Histórico de Preços (guia de estudo)

Este guia acompanha o PR da Fase 7. Até a Fase 6 o sistema vendia produto por produto, não premiava o cliente que volta e não guardava quem mudou o quê. Esta fase traz quatro coisas:

1. **Combos**: um preço especial para um conjunto de produtos (ex.: "Kit Churrasco").
2. **Fidelidade**: o cliente ganha pontos nas compras e pode trocá-los.
3. **Auditoria**: um registro de quem fez cada ação importante, quando, e o que mudou.
4. **Histórico de preços**: o histórico de custo ganha um gráfico com a evolução do custo.

Refinamento visual, ampliação geral de testes e deploy ficam para a Fase 8.

---

## 1. Combos

### O modelo

```
Combos                                   ComboItems
  Id | Name          | SalePrice | Active    Id | ComboId | ProductId | Quantity
   1 | Kit Churrasco |     89,90 | true       1 |       1 |  Heineken |        6
                                            2 |       1 |    Malbec |        1
```

- O combo **não tem estoque próprio**. Ele é só uma "receita": quais produtos e quantos de cada.
- Um produto aparece uma vez só em cada combo (índice único em `ComboId + ProductId`). Para 6 Heinekens, a quantidade é 6, não seis linhas.
- `ComboDto` traz contas prontas para a tela:
  - `NormalPrice` é quanto custariam os produtos separados (preços de venda atuais).
  - `Savings` é `NormalPrice − SalePrice`, e `SavingsPercent` é a economia em %.
  - `AvailableQuantity` é quantos combos dá para montar com o estoque atual. O produto mais escasso manda: com 147 Heinekens e 8 Malbecs, um combo de 6 + 1 tem 8 disponíveis.

### Regra: o combo não pode ser mais caro que os produtos separados

Ninguém compraria, e um combo mais caro daria um "desconto negativo" nos itens. A regra é conferida em dois momentos:

1. **Ao cadastrar ou editar** (`ComboService.EnsurePriceIsNotAboveNormal`): "O preço do combo (R$ 85,00) não pode ser maior que a soma dos produtos separados (R$ 79,40)."
2. **Na venda**: se depois de cadastrado algum produto ficou mais barato, o combo pode ter ficado mais caro que os avulsos. A venda é recusada com uma mensagem pedindo para revisar o preço. Na tela de combos, aparece o selo "Mais caro que avulso"; no PDV, "preço desatualizado".

### Como um combo vira itens da venda

A regra do plano é: **vender um combo baixa o estoque de cada produto individualmente**. A forma mais simples de garantir isso é transformar o combo em itens comuns da venda, um por produto, marcados com `SaleItem.ComboId`:

```
Venda de 1 "Kit Happy Hour" (3 Heineken + 1 Malbec) por R$ 74,90
Separados: 3 × 6,50 + 59,90 = R$ 79,40  →  economia de R$ 4,50

SaleItem  Produto   Qtd  UnitPrice  Discount  Total   ComboId
          Heineken    3       6,50      1,11  18,39   5
          Malbec      1      59,90      3,39  56,51   5
                                         ----  -----
                                         4,50  74,90  ← o preço do combo, exato
```

Por que assim:

- **O estoque não ganha caminho novo.** Cada item passa pelo mesmo `BuildItem` da venda avulsa: lotes por FEFO, `StockService.RegisterMovementAsync` por lote, `SaleItemBatch` com o custo de cada lote. A regra de ouro do projeto continua valendo: nada mexe no `CurrentStock` fora do `StockService`.
- **O cancelamento não muda.** Ele devolve cada `SaleItemBatch` ao seu lote, seja de item avulso ou de combo.
- **Os relatórios não mudam.** Faturamento, CMV e lucro por produto já somam `item.Total`. Como a soma dos itens do combo é exatamente o preço do combo, os números batem sem nenhum caso especial.

### Rateio da economia entre os itens

A economia (R$ 4,50) vira o `Discount` dos itens, proporcional ao valor de cada um. O problema clássico do rateio são os centavos: arredondar cada parte sozinha pode somar R$ 4,49 ou R$ 4,51.

`SaleService.SplitProportionally` arredonda o **acumulado**, e cada parte é a diferença entre um acumulado e o anterior:

```
Heineken: 19,50 de 79,40 → acumulado 4,50 × 19,50/79,40 = 1,105 → 1,11   parte = 1,11
Malbec:   79,40 de 79,40 → acumulado 4,50 × 79,40/79,40 = 4,50  → 4,50   parte = 4,50 − 1,11 = 3,39
```

A última parte "fecha a conta", então a soma é sempre exata.

### O mesmo produto avulso e dentro do combo

2 Heinekens avulsas + 1 combo com 3 Heinekens são 5 unidades do mesmo produto, em itens diferentes. Se cada item escolhesse os lotes sozinho, os dois poderiam contar com as mesmas unidades.

A classe interna `BatchPicker` resolve isso: ela guarda quantas unidades de cada lote já foram reservadas nesta venda, e o próximo item só enxerga o que sobrou. Os itens avulsos são processados primeiro, depois os combos. Se faltar estoque, a mensagem diz o produto e quanto ainda está disponível.

O PDV faz a mesma soma antes de mandar (função `findShortage`), para avisar o vendedor na hora. Mas é só uma prévia: quem decide é a API.

---

## 2. Fidelidade

### A regra

**R$ 10 gastos = 1 ponto**, sempre arredondando para baixo, sobre o **total da venda** (já com desconto). Uma venda de R$ 87,90 dá 8 pontos; os R$ 7,90 que sobram não viram ponto.

O valor por ponto e um botão liga/desliga ficam em **Configurações**, na mesma tabela `SystemSettings` da Fase 6:

```
SystemSettings
  Id | Key                    | Value
   1 | MinimumMarginPercent   | 15
   2 | LoyaltyEnabled         | true
   3 | LoyaltyAmountPerPoint  | 10
```

Mudar a regra vale só para as próximas vendas. Os pontos já ganhos ficam como estão.

### O saldo é sempre a soma do extrato

```
LoyaltyTransactions
  Id | CustomerId | Points | Type       | SaleId | Description
   1 |         12 |     +8 | EARN       |     12 | Venda #12
   2 |         12 |     −5 | REDEEM     |        | Taça grátis
   3 |         12 |     +2 | ADJUSTMENT |        | Compra antiga sem cadastro
   4 |         12 |     −8 | ADJUSTMENT |     12 | Estorno: venda #12 cancelada
                     ----
             saldo = −3
```

Não existe uma coluna "saldo" no cliente. O saldo é `SUM(Points)` do extrato. É a mesma ideia do estoque: um número guardado à parte pode se desencontrar do histórico; uma soma nunca se desencontra.

`Points` tem sinal: positivo soma, negativo tira. `Type` diz o motivo:

| Tipo | Quando | Quem |
|---|---|---|
| `EARN` | Venda concluída com cliente e programa ligado | Automático, na mesma transação da venda |
| `REDEEM` | Cliente troca pontos por algo | ADMIN e VENDEDOR |
| `ADJUSTMENT` | Correção manual, ou estorno de venda cancelada | ADMIN (manual) ou automático (estorno) |

### Regras de cada operação

- **Ganho**: só em venda com cliente e com o programa ligado. Venda sem cliente não gera pontos.
- **Resgate**: não pode passar do saldo ("Saldo insuficiente. O cliente tem 8 ponto(s) e o resgate pede 20.").
- **Ajuste manual**: só ADMIN, com motivo obrigatório, e não pode deixar o saldo negativo.
- **Cancelar a venda**: estorna exatamente os pontos que ela deu, mesmo que a regra tenha mudado depois. Se o cliente já tinha gastado esses pontos, **o saldo fica negativo**, e as próximas compras cobrem a diferença. Bloquear o cancelamento por causa de pontos seria pior: o dinheiro já voltou para o cliente.

---

## 3. Auditoria

### O modelo

```
AuditLogs
  Id | UserId | Action                | Entity  | EntityId | Description     | OldValue                        | NewValue
   7 |      1 | PRODUCT_PRICE_CHANGED | Product |        1 | Heineken 350ml  | {"costPrice":3.9,"salePrice":6} | {"costPrice":3.9,"salePrice":6.5}
```

- `Description` guarda um nome legível ("Heineken 350ml", "Venda #12"), para a tela não precisar buscar o registro, que pode até ter mudado de nome depois.
- `OldValue` e `NewValue` são JSON com só os campos que interessam. A tela compara os dois e mostra "Preço de venda: ~~R$ 6,00~~ → R$ 6,50".

### O que é auditado

| Ação | Onde |
|---|---|
| Ajuste e perda de estoque | `StockService.AdjustAsync` |
| Baixa de lote vencido | `BatchService.WriteOffAsync` |
| Cancelamento de venda | `SaleService.CancelAsync` |
| Mudança de preço de custo ou de venda no cadastro | `ProductService` |
| Produto desativado ou reativado | `ProductService` |
| Usuário cadastrado, alterado, desativado; senha trocada | `UserService`, `AuthService` |
| Combo cadastrado, alterado, desativado | `ComboService` |
| Resgate e ajuste de pontos | `LoyaltyService` |
| Configurações (margem mínima, fidelidade) | `SettingsService` |

Vendas e compras normais **não** entram na auditoria: elas já são o próprio registro (têm usuário, data e itens). Pelo mesmo motivo, o custo que muda por causa de uma compra aparece no histórico de custo, não na auditoria.

### Como funciona: chamada explícita, na mesma transação

```csharp
auditService.Add(AuditAction.PRODUCT_PRICE_CHANGED, nameof(Product), product.Id, product.Name,
    new { costPrice = oldCost, salePrice = oldSale },
    new { costPrice = product.CostPrice, salePrice = product.SalePrice });
await context.SaveChangesAsync();
```

- `AuditService.Add` **só adiciona** o registro ao `DbContext`, não salva. Quem salva é o `SaveChangesAsync` (ou a transação) da própria operação. Resultado: ou a mudança e o registro de auditoria são gravados juntos, ou nenhum dos dois. Nunca existe ajuste de estoque sem auditoria, nem auditoria de algo que falhou.
- O usuário vem do token (`ICurrentUserService`), nunca do corpo da requisição.
- **Senha nunca vai para a auditoria**, nem o hash. Fica só o fato: "Senha alterada" ou `passwordReset: true`.
- **Salvar sem mudar nada não gera registro.** Editar um produto só no nome não registra mudança de preço; salvar as configurações sem mexer em nada também não.

Por que não um "interceptador" automático do EF, que audita toda mudança sozinho? Seria mágico demais para estudar: registraria colunas técnicas, não teria o nome legível nem o motivo, e não saberia distinguir um ajuste manual de uma venda. A chamada explícita é uma linha em cada ação e deixa claro o que está sendo registrado.

A auditoria **só lê**: não existe endpoint para editar ou apagar registros.

---

## 4. Histórico de preços

O histórico de custo existe desde a Fase 2 (`GET /api/products/{id}/cost-history`): uma linha por compra, com a variação % em relação à compra anterior e a variação total desde a primeira compra.

Nesta fase:

- O modal **Custos** da tela de Produtos ganhou um **gráfico de linha** com a evolução do custo, com tooltip mostrando data, compra, fornecedor e variação. A tabela continua embaixo, com os números exatos.
- O eixo X usa a ordem das compras, não a data, porque duas compras podem cair no mesmo dia. O eixo Y não começa no zero de propósito: o que interessa é ver a variação.
- Mudanças de **preço de venda** (e de custo) feitas no cadastro ficam na auditoria (`PRODUCT_PRICE_CHANGED`), com o antes e o depois.

O gráfico usa a Recharts, que é pesada. Por isso ele é carregado sob demanda (`lazy`): só é baixado quando alguém abre o histórico de um produto, e a tela de Produtos continua leve.

---

## 5. Quem vê o quê

| Endpoint | ADMIN | VENDEDOR | ESTOQUISTA |
|---|:-:|:-:|:-:|
| `GET /api/combos` | ✓ | ✓ | 403 |
| `POST/PUT/DELETE /api/combos` | ✓ | 403 | 403 |
| `GET /api/customers/{id}/loyalty` | ✓ | ✓ | 403 |
| `POST /api/customers/{id}/loyalty/redeem` | ✓ | ✓ | 403 |
| `POST /api/customers/{id}/loyalty/adjust` | ✓ | 403 | 403 |
| `GET /api/audit-logs` | ✓ | 403 | 403 |
| `GET /api/settings/loyalty` | ✓ | ✓ | ✓ |
| `PUT /api/settings/loyalty` | ✓ | 403 | 403 |

O vendedor vende combos no PDV e faz resgates no balcão, mas não cria combos, não ajusta pontos e não vê a auditoria. No menu, Combos e Auditoria aparecem só para o ADMIN.

---

## 6. Endpoints

| Método | Rota | O que faz |
|---|---|---|
| GET | `/api/combos?search=&active=&page=&pageSize=` | Lista com preço normal, economia e disponíveis |
| GET | `/api/combos/{id}` | Detalhe |
| POST | `/api/combos` | Cadastra: `{ name, description, salePrice, items: [{ productId, quantity }] }` |
| PUT | `/api/combos/{id}` | Edita; os itens enviados substituem os atuais |
| DELETE | `/api/combos/{id}` | Desativa |
| POST | `/api/sales` | Agora aceita `combos: [{ comboId, quantity }]` junto com `items` |
| GET | `/api/customers/{id}/loyalty?page=&pageSize=` | Saldo, regra atual e extrato |
| POST | `/api/customers/{id}/loyalty/redeem` | Resgate: `{ points, description }` |
| POST | `/api/customers/{id}/loyalty/adjust` | Ajuste (só ADMIN): `{ points, reason }`, positivo soma, negativo tira |
| GET | `/api/audit-logs?entity=&entityId=&action=&userId=&from=&to=&page=&pageSize=` | Auditoria (só ADMIN); datas no horário de Brasília |
| GET | `/api/settings/loyalty` | `{ enabled, amountPerPoint }` |
| PUT | `/api/settings/loyalty` | Altera (só ADMIN) |

`GET /api/sales/{id}` agora traz `loyaltyPoints` (os pontos que a venda deu) e, em cada item, `comboId` e `comboName`.

---

## 7. Backend: arquivos novos e alterados

| Arquivo | O que faz |
|---|---|
| `Entities/Combo.cs`, `ComboItem.cs`, `LoyaltyTransaction.cs`, `AuditLog.cs` | As entidades novas |
| `Entities/Enums/LoyaltyType.cs`, `AuditAction.cs` | Tipos de movimento de pontos e ações auditadas (gravados como texto) |
| `Entities/SaleItem.cs` | Ganhou `ComboId` (nulo para item avulso) |
| `Data/Configurations/...` | Tabelas, índices (`ComboId + ProductId` único; extrato por cliente e data; auditoria por data e por registro) |
| `Data/Seed/SystemSettingSeed.cs` | As duas configurações da fidelidade (ligado, R$ 10) |
| `Data/Migrations/..._Phase7CombosLoyaltyAudit.cs` | Cria `Combos`, `ComboItems`, `LoyaltyTransactions`, `AuditLogs`, a coluna `SaleItems.ComboId` e as configurações |
| `Services/ComboService.cs` | Cadastro, preço normal, economia, disponíveis |
| `Services/LoyaltyService.cs` | Extrato, resgate, ajuste, pontos da venda e estorno |
| `Services/AuditService.cs` | `Add` (só adiciona ao contexto) e a consulta com filtros |
| `Services/SaleService.cs` | Combos na venda (`AddComboItems`, `SplitProportionally`, `BatchPicker`), pontos na venda e estorno no cancelamento |
| `Services/StockService.cs`, `BatchService.cs`, `ProductService.cs`, `UserService.cs`, `AuthService.cs`, `SettingsService.cs` | Uma chamada de auditoria em cada ação importante |
| `Controllers/CombosController.cs`, `AuditLogsController.cs` | Rotas novas |
| `Controllers/CustomersController.cs`, `SettingsController.cs` | Rotas de fidelidade |

---

## 8. Testes

271 testes (eram 219). Os novos:

- `ComboServiceTests`: preço normal, economia e disponíveis; combo mais caro que os avulsos recusado; produto inativo; produto repetido; edição trocando os itens; desativação auditada.
- `SaleComboTests`: o combo vira um item por produto, com a economia rateada e a soma igual ao preço do combo; baixa de estoque por produto e por lote (FEFO); mesmo produto avulso e no combo, sem reservar a mesma unidade duas vezes (e usando o mesmo lote quando há estoque); combo inativo; combo que ficou mais caro que os avulsos; cancelamento devolvendo tudo aos lotes; relatórios contando os itens de combo como vendas dos produtos.
- `LoyaltyServiceTests`: pontos arredondando para baixo, sobre o total com desconto e com o valor configurado; sem cliente ou com o programa desligado, zero pontos; resgate acima do saldo; ajuste só até zero; estorno no cancelamento, inclusive deixando o saldo negativo; cliente inativo ou inexistente.
- `AuditServiceTests`: preço, desativação, ajuste de estoque, baixa de lote, cancelamento e senhas geram o registro certo, com o usuário do token; a senha nunca aparece; editar sem mudar o preço não registra; filtros.
- `SettingsServiceTests`: valores iniciais e validação da fidelidade.
- `AuthorizationTests`: as permissões da seção 5, e que a auditoria mostra quem cadastrou um usuário sem expor a senha.

Também conferi contra o MySQL real: venda com combo e itens avulsos, soma dos itens igual ao subtotal, estoque igual à soma dos lotes, pontos, resgate, estorno e os registros de auditoria.

---

## 9. Frontend

| Peça | Arquivo | O que faz |
|---|---|---|
| API e tipos | `api/combos.ts`, `api/audit.ts`, `api/customers.ts`, `api/settings.ts`, `types/combo.ts`, `types/loyalty.ts`, `types/audit.ts` | Espelho dos DTOs |
| Combos | `pages/combos/CombosPage.tsx`, `ComboFormModal.tsx` | Lista e formulário. Enquanto o ADMIN monta o combo, a tela mostra o preço separado e a economia, e avisa se ficou mais caro |
| PDV | `pages/sales/PosPage.tsx` | Botões "Produtos / Combos", combos no carrinho, saldo de pontos do cliente com a prévia do que a venda dá, e os pontos ganhos na mensagem final |
| Detalhes da venda | `pages/sales/SaleDetailsModal.tsx` | Selo "Combo: ..." nos itens e os pontos da venda (e o aviso de estorno ao cancelar) |
| Fidelidade | `pages/customers/CustomerLoyaltyPanel.tsx` | Aba "Pontos de fidelidade" no histórico do cliente: saldo, extrato, resgate e ajuste (este só para o ADMIN) |
| Auditoria | `pages/audit/AuditPage.tsx`, `auditChanges.ts` | Filtros por ação, tipo, usuário e período; "o que mudou" campo a campo; cartões no celular |
| Gráfico de custo | `components/charts/CostHistoryChart.tsx` | Linha da evolução do custo, carregada sob demanda |
| Configurações | `pages/settings/SettingsPage.tsx` | Novo cartão do programa de fidelidade |

`auditChanges.ts` é a parte mais didática do frontend: ele lê o JSON de antes e depois, descarta os campos que não mudaram e traduz cada campo e valor ("salePrice" vira "Preço de venda", `true` vira "Sim", 6.5 vira "R$ 6,50").

---

## 10. Decisões desta fase

1. **Combo vira itens comuns da venda** (com `ComboId`), em vez de um item "combo" à parte. Estoque, cancelamento e relatórios funcionam sem nenhuma regra nova.
2. **Preço do item de combo = preço avulso, com a economia como desconto.** Assim o relatório mostra quanto se deixou de ganhar com os combos.
3. **Saldo de pontos = soma do extrato**, nunca uma coluna guardada.
4. **Estorno pode deixar o saldo negativo**, em vez de bloquear o cancelamento.
5. **Pontos sobre o total com desconto**: o cliente ganha pelo que pagou de fato.
6. **Auditoria explícita**, uma linha por ação, gravada na mesma transação da mudança.
7. **Sem tela de "histórico de preço de venda" separada**: as mudanças de preço de venda já ficam na auditoria (filtre por "Preço alterado").

---

## 11. Como testar na sua máquina

**1. Atualize o código** (com a API parada, para não travar o build):

```bash
git checkout main
git pull
```

Esta fase não traz pacote novo no frontend (a Recharts já veio na Fase 6).

**2. Aplique a migration** `Phase7CombosLoyaltyAudit`, com o container `adega-mysql` rodando na porta 3307:

```bash
dotnet ef database update --project backend/src/AdegaSystem.Api
```

**3. Rode a API e o frontend:**

```bash
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http
# em outro terminal
cd frontend && npm run dev
```

**Roteiro sugerido:**

1. Entre como **admin** e abra **Combos**. Cadastre um combo com dois produtos. Ponha um preço maior que o "Separados" para ver o aviso, tente salvar (a API recusa) e depois um preço menor.
2. No **PDV**, clique em "Combos", adicione o combo e, em "Produtos", um item avulso do mesmo produto. Escolha um cliente: aparece o saldo e quantos pontos a venda vai dar. Finalize: a mensagem mostra os pontos ganhos.
3. Clique em "Ver detalhes": os itens do combo têm o selo "Combo" e o desconto de cada um; a soma bate com o preço do combo. Em **Estoque**, veja a saída de cada produto.
4. Em **Clientes → Histórico → Pontos de fidelidade**: tente resgatar mais que o saldo (erro), depois resgate alguns pontos e faça um ajuste.
5. No **histórico de vendas**, cancele a venda: os pontos são estornados. Volte ao extrato do cliente: aparece o estorno e, se ele já tinha resgatado, o aviso de saldo negativo.
6. Abra **Auditoria**: estão lá o combo cadastrado, o resgate, o ajuste e o cancelamento, com o antes e o depois. Filtre por ação e por usuário. Edite o preço de venda de um produto e veja o "Preço alterado".
7. Em **Produtos → Custos** de um produto com várias compras: o gráfico mostra a evolução do custo.
8. Em **Configurações**, mude o valor por ponto (tente 0 para ver o erro) ou desligue o programa, e faça uma venda com cliente para ver o efeito. Depois volte para R$ 10.
9. Entre como **vendedor**: o PDV vende combos e o cliente mostra os pontos com o botão "Resgatar", mas o menu não tem Combos nem Auditoria, e não há o botão "Ajustar". Como **estoquista**, nada disso aparece.
