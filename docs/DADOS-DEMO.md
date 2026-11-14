# Dados de demonstração (ambiente local)

Este guia explica como carregar no seu computador uma adega "em operação", com produtos, fornecedores,
clientes, compras, lotes, combos, vendas e usuários de todos os perfis, e como a carga foi construída.

Tudo é **fictício** e **só para desenvolvimento**: marcas inventadas, e-mails `@adegasystem.local`,
CPFs e CNPJs que só passam na conta dos dígitos verificadores, e códigos de barras da faixa de uso interno.

## Sumário

- [Visão geral](#visão-geral)
- [Preparar o banco adega_demo (uma vez)](#preparar-o-banco-adega_demo-uma-vez)
- [Carregar os dados](#carregar-os-dados)
- [Usar o sistema com os dados demo](#usar-o-sistema-com-os-dados-demo)
- [O que você vai encontrar](#o-que-você-vai-encontrar)
- [Recriar o banco demo (reset)](#recriar-o-banco-demo-reset)
- [Como a carga funciona](#como-a-carga-funciona)
- [Proteções](#proteções)
- [Arquivos](#arquivos)

## Visão geral

| Comando | O que faz |
| --- | --- |
| `dotnet run ... -- seed-demo` | Aplica as migrations no `adega_demo`, carrega os dados e **encerra** (não sobe a API) |
| `dotnet run ... -- use-demo` | Sobe a API normalmente, mas usando o `adega_demo` |
| `dotnet run ...` (sem nada) | Uso normal, com o seu banco `adega` de sempre |

O banco da demo é **separado** (`adega_demo`, no mesmo container `adega-mysql` da porta 3307) e tem
**connection string própria** (`ConnectionStrings:DemoConnection`). O seu banco de desenvolvimento normal
nunca é lido nem alterado pelos comandos da demo.

> **Via Docker, sem .NET nem Node:** o `docker-compose.demo.yml` roda este mesmo `seed-demo` num contêiner em
> Development, com um MySQL só para o `adega_demo`, e sobe a API com `use-demo` e o frontend em
> `http://localhost:8081`. As senhas vêm do `.env.demo` em vez dos User Secrets, e as travas abaixo continuam iguais.
> Passo a passo em [DEMO-DOCKER.md](DEMO-DOCKER.md).

## Preparar o banco adega_demo (uma vez)

O usuário `adega` do `.env` só tem permissão no banco `adega`. Crie o banco da demo e dê a permissão
com o usuário `root` do container (a senha é o `MYSQL_ROOT_PASSWORD` do seu `.env`):

```powershell
docker exec -it adega-mysql mysql -uroot -p -e "CREATE DATABASE IF NOT EXISTS adega_demo; GRANT ALL PRIVILEGES ON adega_demo.* TO 'adega'@'%';"
```

Depois configure dois User Secrets (troque `SUA_SENHA` pelo `MYSQL_PASSWORD` do `.env` e escolha uma senha demo
com 8+ caracteres, letras e números):

```powershell
dotnet user-secrets set "ConnectionStrings:DemoConnection" "Server=localhost;Port=3307;Database=adega_demo;User=adega;Password=SUA_SENHA;" --project backend/src/AdegaSystem.Api
dotnet user-secrets set "DemoData:Password" "SUA_SENHA_DEMO" --project backend/src/AdegaSystem.Api
```

- `DemoData:Password` é a senha **dos 4 usuários demo**. Ela não fica no código, no README nem nos logs.
- O admin continua sendo o `admin@adega.local`, com a senha de `Admin:InitialPassword` (a mesma que você já usa).

## Carregar os dados

Com a API **parada** (senão o build falha com o `.exe` travado, erro MSB3027):

```powershell
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http -- seed-demo
```

O comando:

1. confere que está em Development e que a `DemoConnection` aponta para `adega_demo`;
2. aplica no `adega_demo` as migrations que já existem no projeto (nenhuma migration nova foi criada);
3. define a senha do admin, como a API faz ao subir;
4. carrega os dados e mostra um resumo no terminal.

Rodar de novo não duplica nada: aparece "Os dados de demonstração já estavam carregados" e o banco fica como está.

## Usar o sistema com os dados demo

```powershell
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http -- use-demo
```

O frontend (`npm run dev`) não muda: ele conversa com a API, que agora está no banco demo.
Para voltar ao banco normal, pare a API e rode sem o `use-demo`.

| Perfil | Login | Senha |
| --- | --- | --- |
| ADMIN | `admin@adega.local` | a de `Admin:InitialPassword` |
| VENDEDOR | `vendedor1@adegasystem.local`, `vendedor2@adegasystem.local` | a de `DemoData:Password` |
| ESTOQUISTA | `estoquista1@adegasystem.local`, `estoquista2@adegasystem.local` | a de `DemoData:Password` |

## O que você vai encontrar

As datas são **relativas ao dia em que a carga rodou** (D = hoje).

| Tipo | Quantidade | Destaques |
| --- | --- | --- |
| Categorias | 12 | as 10 da migration + Sucos e Cachaças |
| Produtos | 44 | 39 com código de barras, 5 sem (gelo, carvão, copos, artesanais); 2 inativos com histórico |
| Fornecedores | 8 | 1 inativo com compras, 1 sem nenhuma compra |
| Clientes | 25 | 15 com CPF, 18 com telefone, 14 com compras, 1 inativo com histórico |
| Usuários | 5 | o admin + 2 vendedores + 2 estoquistas |
| Compras | 20 | entre D-88 e D-3, 79 lotes, custos diferentes por compra |
| Combos | 8 | 1 inativo |
| Vendas | 36 | entre D-30 e hoje (4 hoje), 1 cancelada, combos, descontos e as 4 formas de pagamento |
| Outras operações | 4 | 2 perdas, 1 baixa de lote vencido, 1 resgate de pontos |

Cenários para testar:

- **Estoque:** alto (Pilsen lata, água), zerado (Energético 2L, Vodka Premium, Néctar de Pêssego),
  exatamente no mínimo (Whisky 12 anos, Gin Pink) e abaixo do mínimo (IPA, Refrigerante Limão, Vinho Rosé,
  Gin London Dry, Suco de Laranja).
- **Validade:** 3 lotes vencidos com saldo (Suco de Uva, Weiss, Guaraná), 1 já baixado (Suco de Laranja),
  lotes vencendo em até 7 dias (Pilsen lata, Energético 473ml, Suco de Laranja), em até 30 (Long Neck,
  Cola 2L, Água com gás, Suco de Uva) e em até 60 (Lager, Cola lata, Tônica).
- **Custo:** Refrigerante Cola 2L comprado a R$ 6,20, depois R$ 6,50 e por último R$ 6,35 (custo atual).
- **FEFO:** vários produtos com 2 a 4 lotes; abra os detalhes de uma venda para ver de qual lote saiu cada unidade.
- **Scanner:** os códigos são EAN-13 começando com `290` (ex.: `2900000000087` é a Cola 2L).
  Para testar pela câmera, gere a imagem do código num site gerador de EAN-13 e aponte o celular para a tela.
- **Dashboard e relatórios:** vendas espalhadas pelos últimos 30 dias, lucro, ranking de produtos e categorias,
  compras por fornecedor, produtos parados.

## Recriar o banco demo (reset)

Não existe comando de reset no sistema, de propósito: nada é apagado automaticamente. Quando quiser renovar a
demo (por exemplo, porque os lotes "vencendo em 5 dias" já venceram), faça à mão, **só no `adega_demo`**:

```powershell
docker exec -it adega-mysql mysql -uroot -p -e "DROP DATABASE adega_demo; CREATE DATABASE adega_demo;"
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http -- seed-demo
```

A permissão do usuário `adega` continua valendo depois de recriar o banco. Confira o nome antes de apertar Enter:
o `DROP DATABASE` é definitivo, e o seu banco normal se chama `adega`.

Se uma carga parar no meio (queda de energia, erro), o comando seguinte se recusa a completar e pede este mesmo reset.

## Como a carga funciona

### Pelos services, nunca por INSERT

A carga não grava nada direto no banco. Cada passo chama o mesmo service que a tela chamaria:

| Dado | Service |
| --- | --- |
| Categorias, fornecedores, produtos, clientes, usuários, combos | `CategoryService`, `SupplierService`, `ProductService`, `CustomerService`, `UserService`, `ComboService` |
| Compras | `PurchaseService` → `StockService.ReceiveBatchAsync` (lote, estoque, movimentação) + histórico de custo |
| Vendas | `SaleService` → FEFO → `SaleItemBatch` → `StockService` → custo, lucro e pontos de fidelidade |
| Cancelamento | `SaleService.CancelAsync` (devolução por lote, estorno de pontos, auditoria) |
| Perdas | `StockService.AdjustAsync` (tipo LOSS) |
| Baixa de vencido | `BatchService.WriteOffAsync` |
| Resgate de pontos | `LoyaltyService.RedeemAsync` |

Por isso a regra de ouro continua valendo: `Product.CurrentStock` nunca é atribuído; ele é sempre a soma dos
lotes, e cada mudança tem a sua `StockMovement`. Os produtos nascem com custo 0 e o custo de verdade vem das compras.

### Quem fez cada operação

Os services descobrem o usuário logado pelo `CurrentUserService`, que lê o token da requisição. Na carga não há
requisição, então o seeder monta um `HttpContext` com o usuário do roteiro (o vendedor que vendeu, o estoquista
que recebeu a compra, o admin que cancelou) só durante aquela operação. O `CurrentUserService` não mudou.

### As datas

Os services gravam tudo com a hora atual (`DateTime.UtcNow`): sem tratamento, as 36 vendas cairiam todas hoje.
A solução, exclusiva da demo, é a [`DemoTimestamps`](../backend/src/AdegaSystem.Api/Data/DemoData/DemoTimestamps.cs):

1. o roteiro ([`DemoTimeline`](../backend/src/AdegaSystem.Api/Data/DemoData/DemoTimeline.cs)) roda em **ordem cronológica**;
2. cada operação é executada normalmente, com a hora atual;
3. logo depois, **toda data gravada por ela** (qualquer data posterior ao início da carga) vai para o dia e a hora do roteiro.

Isso cobre, por exemplo, numa venda: `Sale.SaleDate`, as `StockMovement` de saída e a `LoyaltyTransaction` dos pontos;
num cancelamento: `Sale.CanceledAt`, as devoluções, o estorno de pontos e a `AuditLog`; numa compra:
`Purchase.CreatedAt`, os lotes, as entradas e o `Product.UpdatedAt` do custo. Só colunas de data mudam:
quantidade, lote, custo, lucro, saldo e pontos ficam exatamente como o service calculou.

Como o roteiro é cronológico, cada venda só enxerga os lotes das compras que já aconteceram, e as movimentações
(`PreviousStock` → `NewStock`) formam a mesma sequência que as datas mostram. As vendas de hoje ficam entre a
meia-noite e a hora da carga, nunca no futuro.

Uma diferença consciente: o FEFO decide olhando para o **hoje real**. Um lote que hoje está vencido nunca é vendido,
nem numa venda "de 20 dias atrás", quando ele ainda valia. Não cria inconsistência; é justamente o que deixa os lotes
vencidos com saldo para você ver os alertas.

### Conferência final

Antes de gravar a marca de carga completa (`DemoDataSeededAt` em `SystemSettings`), a
[`DemoChecks`](../backend/src/AdegaSystem.Api/Data/DemoData/DemoChecks.cs) confere que, para todo produto,
estoque = soma dos lotes = soma das movimentações; que todo lote tem a data da sua compra; que nenhuma venda usou
lote que entrou depois dela ou que estava vencido; e que movimentações e pontos têm a data da operação de origem.
Se algo não bater, a carga falha e a marca não é gravada.

## Proteções

| Risco | Proteção |
| --- | --- |
| Rodar em produção | Nada é automático: sem `seed-demo`/`use-demo`, o código da demo nem é chamado. O `docker-compose.prod.yml` não passa esses argumentos. E os dois comandos recusam qualquer ambiente que não seja Development (no `Program.cs` e de novo dentro do seeder) |
| Atingir o banco normal | Connection string própria (`DemoConnection`), que precisa apontar para um banco chamado exatamente `adega_demo`; a `DefaultConnection` não pode apontar para ele; e o comando confere o nome do banco aberto antes de aplicar as migrations |
| Duplicar dados | Marca `DemoDataSeededAt`: a segunda execução não grava nada |
| Misturar com dados existentes | A carga só roda num banco sem produtos, fornecedores, clientes, compras, vendas ou usuários além do admin |
| Senha no repositório | `DemoData:Password` só nos User Secrets; sem ela (ou fraca), a carga não começa |
| Reset destrutivo | Não existe no sistema; é um passo manual e explícito, só no `adega_demo` |

## Arquivos

| Arquivo | Papel |
| --- | --- |
| `Data/DemoData/DemoDatabase.cs` | Modos `seed-demo`/`use-demo` e as travas da connection string |
| `Data/DemoData/DemoDataCommand.cs` | O comando `seed-demo`: migrations, senha do admin, carga e resumo |
| `Data/DemoData/DemoDataSeeder.cs` | Orquestra a carga pelos services, simula o usuário e aplica as datas |
| `Data/DemoData/DemoCatalog.cs` | Cadastros: categorias, fornecedores, produtos, clientes, usuários e combos |
| `Data/DemoData/DemoTimeline.cs` | Roteiro: compras, vendas, perdas, baixa e resgate, dia a dia |
| `Data/DemoData/DemoTimestamps.cs` | Move as datas de cada operação para o dia do roteiro |
| `Data/DemoData/DemoChecks.cs` | Conferência final de estoque e datas |
| `Program.cs` | Escolhe a connection string e desvia para o `seed-demo` |
| `tests/.../DemoData/*Tests.cs` | 47 testes: travas, repetição, consistência, FEFO, datas e cenários |

Para mudar a demo, edite `DemoCatalog.cs` e `DemoTimeline.cs`, rode os testes da pasta `DemoData`
(eles conferem os cenários de estoque e validade) e recrie o `adega_demo`.
