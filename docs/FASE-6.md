# Fase 6: Dashboard, Relatórios e Gráficos (guia de estudo)

Este guia acompanha o PR da Fase 6. Até a Fase 5 o sistema registrava tudo (compras, lotes, vendas, custos), mas não respondia às perguntas do dono: **quanto vendi? quanto lucrei? o que sai mais? o que está parado? algum produto está vendendo com margem baixa?** Esta fase transforma os dados que já existiam em números e gráficos, sem criar nenhuma regra nova de estoque ou de venda.

Combos, fidelidade e auditoria continuam fora (Fase 7).

---

## 1. Relatório só lê

Nenhum endpoint desta fase grava nada (a única escrita é a configuração da margem mínima). Todos os números saem de tabelas que já existiam:

| Pergunta | De onde vem |
|---|---|
| Faturamento, descontos, ticket médio | `Sales` (`Total`, `Discount`) |
| Custo das mercadorias vendidas (CMV) e lucro | `Sales.TotalCost` e `Sales.Profit`, que a Fase 4 já calculava com o custo real de cada lote (`SaleItemBatch`) |
| Produtos e categorias mais vendidos | `SaleItems` |
| Produtos parados | `Products` + a data da última venda |
| Margem por produto | `Products.CostPrice` e `SalePrice` |
| Compras por fornecedor | `Purchases` |
| Valor do estoque | `Products.CurrentStock` × custo/preço |

**Venda cancelada nunca entra.** Toda consulta filtra `Status == COMPLETED`. Uma venda cancelada devolveu as unidades aos lotes, então contá-la seria inventar faturamento.

---

## 2. O período e o fuso de Brasília

Os filtros chegam como dias (`from=2026-10-01&to=2026-10-31`), mas as vendas são gravadas com data e hora **UTC**. Uma venda às 22h de 1º de outubro em Brasília está gravada como 01h de **2 de outubro** em UTC. Se o relatório comparasse as datas direto, essa venda cairia no dia errado.

A solução usa o `BusinessClock` que já existia:

```
de 01/10 até 31/10 (Brasília)
  → StartOfDayUtc(01/10) = 01/10 03:00 UTC
  → StartOfDayUtc(01/11) = 01/11 03:00 UTC  (dia seguinte ao "até", exclusivo)
```

`ReportService.UtcRange` faz essa conta. O banco filtra `CreatedAt >= início && CreatedAt < fim`, e cada venda carregada ganha o seu **dia em Brasília** (`BusinessClock.ToBusinessDate`) para ser agrupada.

Regras do período (`Validators/ReportValidators.cs`):

- Sem datas, o padrão são os últimos 30 dias (hoje e os 29 anteriores).
- "Até" não pode ser antes de "de": `A data final deve ser igual ou posterior à data inicial.`
- No máximo 2 anos por consulta: `O período pode ter no máximo 2 anos.`

Compras são diferentes: `PurchaseDate` já é um dia (`DateOnly`, a data da nota), então não há fuso para converter.

---

## 3. Por que somar em C# e não em SQL?

Cada relatório faz **uma consulta leve** ao banco (só as colunas necessárias, já filtradas por período e status) e depois agrupa e soma em C# com LINQ:

```csharp
var sales = await LoadSalesAsync(from, to);      // SQL: só vendas concluídas do período
var periods = sales.GroupBy(s => PeriodKey(s.LocalDate, groupBy));   // C#: agrupa pelo dia de Brasília
```

Motivos:

1. **O fuso**: agrupar por dia de Brasília em SQL exigiria funções de data específicas do MySQL (`CONVERT_TZ`), que o SQLite dos testes não tem.
2. **Legibilidade**: o LINQ em memória é o mesmo C# de sempre, fácil de ler e de depurar.
3. **Volume**: uma adega faz algumas centenas de vendas por mês. Dois anos de vendas, só com 6 colunas cada, cabem tranquilamente na memória.

Se um dia o volume crescer muito, o caminho é mover os agrupamentos para o SQL (ou criar tabelas de resumo). Não vale pagar essa complexidade agora.

---

## 4. As contas

### Resumo de vendas

| Número | Conta |
|---|---|
| Faturamento | soma de `Sale.Total` (já com desconto) |
| Descontos | soma de `Sale.Discount` |
| Custo (CMV) | soma de `Sale.TotalCost` |
| Lucro | soma de `Sale.Profit` (= faturamento − CMV) |
| Ticket médio | faturamento ÷ número de vendas |
| Margem | lucro ÷ faturamento × 100 |

Valores em dinheiro são arredondados para 2 casas (`MidpointRounding.AwayFromZero`, o arredondamento "de escola"). Percentuais também com 2 casas.

### O desconto rateado entre os produtos

O desconto é dado na venda inteira, não em cada item. Para saber quanto **cada produto** faturou, o desconto é dividido proporcionalmente:

```
Venda: Cerveja R$ 20,00 + Vinho R$ 80,00 = subtotal R$ 100,00, desconto R$ 10,00, total R$ 90,00
Faturamento da cerveja = 20,00 × 90 / 100 = R$ 18,00
Faturamento do vinho   = 80,00 × 90 / 100 = R$ 72,00
```

Assim a soma do faturamento por produto bate com o faturamento total. O custo do produto é `Quantity × CostPrice` do item (que a Fase 4 já gravou com o custo médio dos lotes usados).

### Gráfico sem buracos

Num período de 30 dias, os dias sem venda também aparecem (com zero). `PeriodKeys` gera todos os dias (ou meses) do período antes de juntar com as vendas. Sem isso, o gráfico "pularia" os dias vazios e daria a impressão de que vendeu todo dia.

---

## 5. Produtos mais vendidos, menos vendidos e parados

| Relatório | Regra |
|---|---|
| **Mais vendidos** | produtos que venderam no período, ordenados por quantidade (empate: faturamento) |
| **Menos vendidos** | os mesmos produtos, na ordem contrária. Só entra quem vendeu **pelo menos uma unidade**: quem não vendeu nada é "parado" |
| **Parados** | produtos **ativos com estoque** que não vendem há pelo menos N dias (30, 60 ou 90) |

Nos parados, "dias sem venda" conta a partir da última venda concluída. Produto que **nunca vendeu** conta a partir do cadastro (`CreatedAt`): um produto cadastrado ontem não está "parado há 300 dias". O relatório também mostra o **valor parado**: estoque × custo, o dinheiro que está na prateleira sem girar.

A última venda vem de uma subconsulta correlacionada:

```csharp
LastSaleDate = context.SaleItems
    .Where(i => i.ProductId == p.Id && i.Sale.Status == SaleStatus.COMPLETED)
    .Max(i => (DateTime?)i.Sale.CreatedAt)
```

O `(DateTime?)` faz o `Max` devolver `null` quando não há venda nenhuma, em vez de dar erro.

---

## 6. Margem mínima configurável

O plano previa a entidade `SystemSetting` desde o início: uma tabela chave/valor para configurações do sistema.

```
SystemSettings
  Id | Key                    | Value
   1 | MinimumMarginPercent   | 15
```

- A migration `Phase6SystemSettings` cria a tabela, o índice único em `Key` e já insere a margem mínima de 15%.
- O valor é texto, gravado com `CultureInfo.InvariantCulture` (sempre "15.5", nunca "15,5"), para não depender do idioma do servidor.
- `SettingsService.GetMarginAsync` lê a configuração (se a linha sumir, usa 15). `UpdateMarginAsync` valida (de 0 a menos de 100%) e grava.

Onde o alerta **"Margem abaixo do mínimo configurado."** aparece:

1. **Produtos**: a margem fica num selo vermelho, com a mensagem ao passar o mouse.
2. **Formulário de produto**: enquanto você digita os preços, aparece o aviso embaixo da prévia da margem.
3. **Relatório de margens**: selo "Abaixo do mínimo", contador e filtro "só abaixo do mínimo".

A margem usa a mesma fórmula da Fase 1, `(venda − custo) ÷ venda × 100`, pela mesma função (`ProductMappings.CalculateMarginPercent`), para o relatório nunca discordar da tela de produtos.

---

## 7. Quem vê o quê

Faturamento e lucro são informação de gestão: **dashboard e relatórios são só do ADMIN**.

| Endpoint | ADMIN | VENDEDOR | ESTOQUISTA |
|---|:-:|:-:|:-:|
| `GET /api/dashboard` | ✓ | 403 | 403 |
| `GET /api/reports/...` | ✓ | 403 | 403 |
| `GET /api/settings/margin` | ✓ | ✓ | ✓ |
| `PUT /api/settings/margin` | ✓ | 403 | 403 |

A leitura da margem mínima é liberada para todos porque o estoquista também cadastra produtos e precisa ver o alerta no formulário.

No frontend, VENDEDOR e ESTOQUISTA continuam com a página inicial, mas no lugar do dashboard veem atalhos para o que usam no dia a dia (no menu, o item se chama "Início"). Se digitarem `/relatorios` na barra de endereço, aparece "Acesso negado".

---

## 8. Endpoints

| Método | Rota | O que devolve |
|---|---|---|
| GET | `/api/dashboard` | Cards, vendas dos últimos 30 dias, últimos 12 meses, top 5 produtos, categorias e formas de pagamento do mês |
| GET | `/api/reports/sales?from=&to=&groupBy=Day\|Month` | Resumo, série por dia ou mês e formas de pagamento |
| GET | `/api/reports/products?from=&to=&order=MostSold\|LeastSold&limit=` | Ranking de produtos (limit de 1 a 100, padrão 20) |
| GET | `/api/reports/categories?from=&to=` | Vendas por categoria, com participação % |
| GET | `/api/reports/idle-products?days=30` | Produtos com estoque sem venda há N dias (1 a 365) |
| GET | `/api/reports/margins?onlyBelowMinimum=` | Margem de cada produto ativo e a margem mínima |
| GET | `/api/reports/purchases-by-supplier?from=&to=` | Total comprado por fornecedor |
| GET | `/api/reports/stock` | Unidades e valor do estoque (a custo e a preço de venda), por categoria |
| GET | `/api/settings/margin` | Margem mínima atual |
| PUT | `/api/settings/margin` | Altera a margem mínima (só ADMIN) |

### Os cards do dashboard

| Card | Regra (igual à das telas existentes) |
|---|---|
| Vendas e faturamento de hoje | vendas concluídas do dia em Brasília |
| Faturamento e lucro do mês | do dia 1 até hoje |
| Produtos cadastrados / itens em estoque | produtos ativos / soma do `CurrentStock` |
| Estoque baixo | `CurrentStock <= MinimumStock` (o mesmo filtro de Produtos) |
| Sem estoque | `CurrentStock == 0` |
| Próximos do vencimento | produtos com lote com saldo vencendo de hoje até 30 dias |
| Vencidos | produtos com lote vencido ainda com saldo |

Os cards de alerta têm um link direto para a tela que resolve o problema (Produtos ou Lotes).

---

## 9. Backend: arquivos novos e alterados

| Arquivo | O que faz |
|---|---|
| `Entities/SystemSetting.cs` | Chave/valor de configuração |
| `Data/Configurations/SystemSettingConfiguration.cs` + `Data/Seed/SystemSettingSeed.cs` | Tabela, índice único e o valor inicial (15%) |
| `Data/Migrations/..._Phase6SystemSettings.cs` | Cria a tabela `SystemSettings` |
| `Common/BusinessClock.cs` | Novos `ToBusinessDate` (UTC → dia em Brasília) e `FirstDayOfMonth` |
| `DTOs/Reports/ReportDtos.cs`, `DTOs/Dashboard/DashboardDtos.cs`, `DTOs/Settings/SettingsDtos.cs` | Filtros e respostas |
| `Validators/ReportValidators.cs`, `Validators/SettingsValidators.cs` | Período, limites e margem |
| `Services/ReportService.cs` | Todos os relatórios. As consultas base (`LoadSalesAsync`, `LoadItemsAsync`) ficam no fim do arquivo |
| `Services/DashboardService.cs` | Monta o dashboard **reaproveitando o `ReportService`** (não duplica nenhuma conta) e conta os cards de estoque |
| `Services/SettingsService.cs` | Lê e grava a margem mínima |
| `Controllers/ReportsController.cs`, `DashboardController.cs`, `SettingsController.cs` | Rotas, com `[Authorize(Roles = Roles.Admin)]` |

---

## 10. Testes

219 testes (eram 178). Os novos:

- `ReportServiceTests`: faturamento, CMV, lucro e margem; venda cancelada fora; venda das 22h de Brasília caindo no dia certo; dias sem venda com zero; agrupamento por mês; desconto rateado; mais e menos vendidos; parados (com e sem venda, nunca vendido contando do cadastro, produto sem estoque fora, venda cancelada não conta como venda); margens abaixo do mínimo; compras por fornecedor; valor do estoque; período inválido.
- `SettingsServiceTests`: valor inicial de 15%, gravação com ponto (sem depender do idioma do servidor) e limites (negativo e 100% ou mais).
- `DashboardServiceTests`: cards de vendas e de estoque (baixo, zerado, vencendo, vencido) e um ponto por dia e por mês nos gráficos.
- `AuthorizationTests`: dashboard e relatórios dão 403 para VENDEDOR e ESTOQUISTA; período inválido devolve 400 com o erro no campo.

Os números também foram conferidos contra o MySQL real: a soma feita direto no banco bateu com a API.

---

## 11. Frontend

| Peça | Arquivo | O que faz |
|---|---|---|
| API | `api/reports.ts`, `api/settings.ts`, `types/report.ts` | Chamadas e tipos espelhando os DTOs |
| Dashboard | `pages/dashboard/DashboardPage.tsx` | Cards, gráfico de vendas (7/30 dias), faturamento × lucro mensal e rankings |
| Página inicial | `pages/HomePage.tsx` | ADMIN vê o dashboard; os outros perfis, atalhos |
| Relatórios | `pages/reports/ReportsPage.tsx` + uma aba por arquivo | Vendas, Produtos, Categorias, Parados, Margens, Compras e Estoque |
| Configurações | `pages/settings/SettingsPage.tsx` | Margem mínima |
| Gráficos | `components/charts/` | `SalesCharts.tsx` (barras por período), `RankingChart.tsx` (barras horizontais), `ChartTooltip.tsx` e `chartTheme.ts` (cores) |
| Peças comuns | `components/StatCard.tsx`, `components/PeriodFilter.tsx`, `utils/period.ts` | Card de número, filtro Hoje / 7 dias / 30 dias / Mês atual / Personalizado |
| Hooks | `hooks/useReport.ts`, `hooks/useMinimumMargin.ts` | Carregar um relatório quando o filtro muda; ler a margem mínima |

### O hook `useReport`

Toda aba faz a mesma coisa: carrega, mostra "carregando", mostra erro, recarrega quando o filtro muda. Em vez de repetir isso 7 vezes, o hook concentra:

```tsx
const report = useReport(() => reportsApi.categories(period), [period])
```

Ele também resolve um problema sutil: se você clicar "7 dias" e logo depois "30 dias", a resposta de "7 dias" pode chegar **depois**. A variável `ignore` descarta a resposta antiga para a tela não mostrar o filtro errado.

### Gráficos com Recharts

Recharts é a biblioteca de gráficos que estava no plano (versão 3, a estável mais recente). Regras seguidas:

- **Um eixo só.** Faturamento e lucro estão em reais, então dividem o mesmo eixo. Nunca dois eixos Y com escalas diferentes, que enganam a leitura.
- **Duas cores fixas**: azul para faturamento e laranja para lucro, escolhidas para serem distinguíveis também por quem tem daltonismo. A cor segue o dado, nunca a posição.
- **Tooltip em todos os gráficos**: passe o mouse para ver o valor exato. No eixo, os valores ficam compactos (R$ 1,2 mil).
- Textos sempre em cinza/preto, nunca na cor da série; grade só horizontal e clarinha.
- Toda informação do gráfico também está numa tabela logo abaixo.

### Carregamento sob demanda (lazy)

A Recharts é grande (~370 KB). Com `React.lazy`, o código do dashboard, dos relatórios e das configurações só é baixado quando um ADMIN abre essas telas:

```tsx
const ReportsPage = lazy(() => import('./pages/reports/ReportsPage').then((m) => ({ default: m.ReportsPage })))
```

O vendedor, que só usa o PDV, nunca baixa a biblioteca de gráficos. As abas dos relatórios usam `mountOnEnter`: cada aba só consulta a API quando é aberta.

---

## 12. Decisões desta fase

1. **Somar em C#, não em SQL** (seção 3): legível, testável no SQLite e correto no fuso.
2. **Dashboard e relatórios só para o ADMIN**, como previa o plano.
3. **Desconto rateado** entre os itens para o faturamento por produto bater com o total.
4. **"Menos vendidos" ≠ "parados"**: quem não vendeu nada aparece nos parados, não no fim do ranking.
5. **Parado conta do cadastro** quando o produto nunca vendeu.
6. **Margem mínima no banco** (`SystemSetting`), editável pela tela, em vez de fixa no `appsettings`.
7. **Sem exportação para PDF/Excel nesta fase**: as tabelas podem ser copiadas; exportar fica como melhoria futura.

---

## 13. Como testar na sua máquina

**1. Atualize o código** (com a API parada, para não travar o build):

```bash
git checkout main
git pull
cd frontend && npm install && cd ..
```

O `npm install` baixa a Recharts.

**2. Aplique a migration** (cria a tabela `SystemSettings`), com o container `adega-mysql` rodando na porta 3307:

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

1. Entre como **admin**: a página inicial agora é o dashboard. Confira os cards de vendas com o histórico de vendas, e clique nos alertas de estoque e validade.
2. Passe o mouse nos gráficos e troque entre "7 dias" e "30 dias".
3. Faça uma venda com desconto no PDV e volte ao dashboard: o faturamento de hoje sobe pelo total com desconto.
4. Em **Relatórios**, percorra as abas. Em Vendas, troque para "Por mês" e use "Personalizado". Cancele uma venda e veja que ela some dos números.
5. Em **Configurações**, tente salvar 150% (aparece o erro) e depois um valor alto, como 45%. Volte a **Produtos**: as margens abaixo disso ficam em vermelho. Abra "Editar" e mude o preço de venda para ver o aviso ao vivo. Na aba **Margens** dos relatórios, ligue "só abaixo do mínimo". Depois volte a margem para 15%.
6. Entre como **vendedor** e como **estoquista**: a página inicial mostra atalhos, o menu não tem Relatórios nem Configurações, e `/relatorios` mostra "Acesso negado".
7. No Swagger, chame `GET /api/reports/sales?from=2026-10-31&to=2026-10-01`: 400 com a mensagem do período.
