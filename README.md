# Adega System

> Este repositório contém a versão pública de entrega do Adega System para avaliação acadêmica.

Sistema web de gestão para adegas e distribuidoras de bebidas: catálogo, compras com lotes e validade, estoque,
PDV, vendas, clientes com fidelidade, dashboard e relatórios.

**Versão 1.0.0** · ASP.NET Core (.NET 10) · React 19 + TypeScript · MySQL 9.7 · [CHANGELOG](CHANGELOG.md) · [Notas da release](docs/RELEASE-v1.0.0.md)

## Objetivo

Dar a uma adega pequena ou média um sistema único para o dia a dia: saber o que tem no estoque (e o que está para
vencer), registrar o que chega dos fornecedores, vender no balcão com rapidez e saber, no fim do mês, quanto vendeu e
quanto lucrou de verdade, com o custo real de cada lote.

Duas regras guiam o sistema inteiro:

- **O estoque nunca muda "por fora".** Compra, venda, cancelamento, ajuste, perda e vencimento passam todos pelo mesmo
  serviço (`StockService`), que atualiza o lote, o estoque do produto e grava a movimentação na mesma transação.
- **Na venda, sai primeiro o lote que vence primeiro (FEFO).** Lote vencido nunca é vendido.

## Sumário

- [Funcionalidades da v1.0.0](#funcionalidades-da-v100)
- [Screenshots](#screenshots)
- [Tecnologias](#tecnologias)
- [Arquitetura](#arquitetura)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Perfis de acesso](#perfis-de-acesso)
- [Como rodar localmente](#como-rodar-localmente)
- [Testes e verificação](#testes-e-verificação)
- [Deploy (produção com Docker)](#deploy-produção-com-docker)
- [Documentação](#documentação)
- [Limitações conhecidas](#limitações-conhecidas)
- [Roadmap (depois da v1.0.0)](#roadmap-depois-da-v100)

## Funcionalidades da v1.0.0

**Cadastros e compras**

- **Categorias e Produtos:** cadastro, busca por nome, marca ou código de barras, filtros, margem calculada e alerta de margem abaixo do mínimo configurado.
- **Fornecedores:** cadastro com validação de CNPJ e resumo (total comprado, última compra, produtos mais comprados).
- **Compras:** vários produtos por compra, totais calculados pela API, nota fiscal única por fornecedor; cada item vira um lote e dá entrada no estoque.
- **Cadastro rápido de produto na compra:** se o produto não existe, ele é cadastrado sem sair da tela da compra.
- **Histórico de custos:** cada compra grava o custo pago; o produto mostra a evolução em tabela e gráfico.

**Estoque**

- **Estoque e movimentações:** extrato com filtros e ajustes manuais (correção de inventário e perdas, com motivo obrigatório).
- **Lotes:** número, fabricação, validade e custo por lote; o estoque do produto é sempre a soma dos lotes.
- **Validade:** lotes vencidos e vencendo em 7, 30 ou 60 dias, com baixa de lotes vencidos.
- **FEFO:** a venda baixa automaticamente dos lotes que vencem primeiro.

**Vendas**

- **Clientes:** cadastro com CPF opcional e validado, histórico de compras e ticket médio.
- **PDV:** busca por nome ou leitor de código de barras, combos, cliente, forma de pagamento e desconto, com atalhos F2 (buscar) e F9 (finalizar).
- **Leitor de código de barras pela câmera:** no cadastro de produto, no PDV, na pesquisa de Produtos e na consulta rápida da tela inicial ("Consultar produto": preço, estoque e, para ADMIN/ESTOQUISTA, os próximos lotes), pelo celular ou pela webcam (EAN-13, EAN-8, UPC-A, UPC-E). Exige HTTPS fora do `localhost`. Detalhes em [docs/LEITOR-CODIGO-DE-BARRAS.md](docs/LEITOR-CODIGO-DE-BARRAS.md).
- **Vendas:** histórico com filtros e detalhes (itens, lotes usados, custo e lucro).
- **Cancelamento de vendas:** com motivo, só pelo ADMIN; cada unidade volta ao lote de origem e os pontos de fidelidade são estornados.
- **Lucro e custos:** o lucro de cada venda usa o custo real dos lotes que saíram, com o desconto rateado entre os itens.
- **Combos:** kits com preço especial; no PDV o combo vira um item por produto e o estoque baixa produto a produto.
- **Fidelidade:** pontos por valor gasto (padrão R$ 10 = 1 ponto), resgate no balcão e ajuste manual pelo ADMIN.

**Gestão e segurança**

- **Autenticação JWT:** login com e-mail e senha, sessão de 8 horas, senhas guardadas só como hash.
- **Perfis ADMIN, VENDEDOR e ESTOQUISTA:** cada perfil vê apenas o menu e as ações que pode usar, e a API confere o perfil em toda requisição.
- **Dashboard:** vendas e faturamento do dia, faturamento e lucro do mês, alertas de estoque e validade, gráficos e rankings.
- **Relatórios:** vendas, produtos mais e menos vendidos, categorias, produtos parados, margens, compras por fornecedor e valor do estoque.
- **Auditoria:** quem fez o quê e quando, com os valores de antes e depois (ajustes de estoque, cancelamentos, preços, usuários, pontos e configurações).
- **Tema claro, escuro ou do sistema:** escolhido na barra superior e guardado no navegador.

## Screenshots

Capturas reais do sistema v1.0.0 rodando localmente, com dados de exemplo cadastrados pelas próprias telas e pela API.

| | |
| --- | --- |
| **Login**<br>![Tela de login](docs/telas/v1/light-login.jpg) | **Dashboard**<br>![Dashboard com cards de vendas, alertas de estoque e gráficos](docs/telas/v1/light-dashboard.jpg) |
| **Produtos**<br>![Lista de produtos com preço, margem e estoque](docs/telas/v1/light-produtos.jpg) | **Compras**<br>![Lista de compras por fornecedor e nota fiscal](docs/telas/v1/light-compras.jpg) |
| **Lotes e validade**<br>![Lotes ordenados pela validade, com destaque para os que vencem logo](docs/telas/v1/light-lotes.jpg) | **Estoque (movimentações)**<br>![Extrato de movimentações de estoque por lote](docs/telas/v1/light-estoque.jpg) |
| **PDV**<br>![PDV com carrinho, forma de pagamento e total](docs/telas/v1/light-pdv.jpg) | **Relatórios**<br>![Relatório de vendas com faturamento, CMV, lucro e gráfico](docs/telas/v1/light-relatorios.jpg) |

### Modo claro e modo escuro

| Modo claro | Modo escuro |
| --- | --- |
| ![Dashboard no modo claro](docs/telas/v1/light-dashboard.jpg) | ![Dashboard no modo escuro](docs/telas/v1/dark-dashboard.jpg) |
| ![PDV no modo claro](docs/telas/v1/light-pdv.jpg) | ![PDV no modo escuro](docs/telas/v1/dark-pdv.jpg) |

<details>
<summary>Todas as telas no modo escuro</summary>

| | |
| --- | --- |
| ![Login no modo escuro](docs/telas/v1/dark-login.jpg) | ![Produtos no modo escuro](docs/telas/v1/dark-produtos.jpg) |
| ![Compras no modo escuro](docs/telas/v1/dark-compras.jpg) | ![Lotes no modo escuro](docs/telas/v1/dark-lotes.jpg) |
| ![Estoque no modo escuro](docs/telas/v1/dark-estoque.jpg) | ![Relatórios no modo escuro](docs/telas/v1/dark-relatorios.jpg) |

</details>

<details>
<summary>Cadastro rápido de produto na compra e versão para celular</summary>

![Produto não encontrado na busca da compra e o cadastro no modal](docs/telas/compra-cadastro-rapido.jpg)

![Página inicial no celular: antes da etapa de UI/UX, depois no claro e no escuro](docs/telas/celular-home.jpg)

</details>

## Tecnologias

| Camada | Tecnologia | Versão |
| --- | --- | --- |
| Backend | .NET / ASP.NET Core (Web API) | 10 (LTS) |
| ORM | Entity Framework Core | 10.0.12 |
| Provider MySQL | MySql.EntityFrameworkCore (oficial da Oracle) | 10.0.9 |
| Validação | FluentValidation | 12.1.1 |
| Autenticação | JWT Bearer + PasswordHasher do ASP.NET Core | 10.0.12 |
| Documentação da API | OpenAPI (Microsoft.AspNetCore.OpenApi) + Swagger UI (Swashbuckle) | 10.0.12 / 10.2.3 |
| Banco de dados | MySQL (imagem Docker `mysql:9.7.2`) | 9.7 LTS |
| Frontend | React + Vite + TypeScript | 19.3 / 8.3 / 7.0 |
| UI | Bootstrap + React Bootstrap + Bootstrap Icons | 5.3.8 / 2.10.10 / 1.13.1 |
| Rotas e HTTP | React Router + Axios | 8.4 / 1.20 |
| Gráficos | Recharts | 3.10 |
| Lint do frontend | Oxlint | 1.81 |
| Testes | xUnit v3 + SQLite em memória + Microsoft.AspNetCore.Mvc.Testing | 4.0.1 / 10.0.12 |
| Produção | Docker Compose + nginx | nginx 1.30 |

## Arquitetura

```
React + TypeScript (navegador)
        │  HTTP/JSON com token JWT (Axios)
        ▼
ASP.NET Core Web API
        │  Controllers: recebem a requisição, validam (FluentValidation) e chamam os services
        ▼
Services (regras de negócio: StockService, SaleService, PurchaseService, ReportService...)
        │
        ▼
EF Core (AdegaDbContext, migrations)
        │
        ▼
MySQL
```

- **Um único projeto de Web API**, `backend/src/AdegaSystem.Api`, organizado por pastas (`Controllers`, `Services`,
  `Validators`, `DTOs`, `Entities`, `Data`, `Auth`...), e **um projeto de testes**, `backend/tests/AdegaSystem.Tests`.
- Os controllers só cuidam de HTTP. As regras ficam nos services, que usam o `AdegaDbContext` diretamente (sem camada
  de repositório genérico).
- Operações críticas (compra, venda, cancelamento, ajuste, resgate de pontos) rodam numa transação: ou grava tudo, ou nada.
- O frontend é uma SPA em `frontend/`, com uma pasta por tela em `src/pages` e uma função por endpoint em `src/api`.
- Em desenvolvimento, o Vite (porta 5173) repassa `/api` e `/health` para a API (porta 5080). Em produção, o nginx faz o
  mesmo papel e serve os arquivos do React.

O plano completo, com entidades e decisões, está em [docs/PLANO.md](docs/PLANO.md).

## Estrutura de pastas

```
adega-system-tcc/
├── AdegaSystem.slnx             # solution .NET
├── global.json                  # fixa o SDK .NET 10
├── dotnet-tools.json            # ferramentas locais (dotnet-ef)
├── docker-compose.yml           # MySQL para desenvolvimento (porta 3307)
├── docker-compose.prod.yml      # produção: MySQL + API + frontend (nginx)
├── .env.example                 # variáveis de exemplo do docker-compose (desenvolvimento)
├── .env.prod.example            # modelo das variáveis de produção (copie para .env.prod)
├── CHANGELOG.md
├── docs/                        # plano, guias de cada fase, API, release notes e screenshots
├── backend/
│   ├── src/AdegaSystem.Api/     # ASP.NET Core Web API
│   │   ├── Controllers/         # só HTTP: recebem DTOs e chamam os services
│   │   ├── Services/            # regras de negócio
│   │   ├── Validators/          # validação de entrada (FluentValidation)
│   │   ├── DTOs/                # contratos de entrada e saída da API
│   │   ├── Mappings/            # entidade → DTO
│   │   ├── Entities/            # tabelas do banco
│   │   ├── Data/                # AdegaDbContext, Configurations (Fluent API), Migrations, Seed
│   │   ├── Auth/                # JWT, perfis (Roles) e políticas de acesso
│   │   ├── Common/              # ApiResponse, PagedResult, exceções de negócio
│   │   ├── Middleware/          # GlobalExceptionHandler
│   │   ├── Dockerfile           # imagem de produção da API
│   │   └── Program.cs
│   └── tests/AdegaSystem.Tests/ # regras de negócio, permissões e API por HTTP
└── frontend/
    ├── Dockerfile, nginx.conf   # imagem de produção (nginx serve o React e repassa /api)
    └── src/
        ├── api/                 # Axios (com o token JWT) + uma função por endpoint
        ├── auth/                # sessão, RequireAuth e permissões por perfil
        ├── components/          # componentes reutilizáveis (tabelas, pickers, gráficos, alertas...)
        ├── layouts/             # layout com menu lateral (o menu é definido em menu.ts)
        ├── pages/               # uma pasta por tela
        ├── theme/, styles/      # tema claro/escuro e cores (tokens.css)
        └── hooks/, types/, utils/
```

## Perfis de acesso

| Área | ADMIN | VENDEDOR | ESTOQUISTA |
| --- | :-: | :-: | :-: |
| Produtos e categorias: consultar | ✓ | ✓ | ✓ |
| Produtos e categorias: cadastrar, editar, desativar | ✓ | | ✓ |
| Estoque, lotes, compras, fornecedores | ✓ | | ✓ |
| PDV, vendas e clientes | ✓ | ✓ | |
| Combos: vender no PDV | ✓ | ✓ | |
| Fidelidade: ver extrato e resgatar pontos | ✓ | ✓ | |
| Cancelar venda, desativar cliente | ✓ | | |
| Usuários, dashboard, relatórios e configurações | ✓ | | |
| Combos: cadastrar e editar; auditoria; ajuste de pontos | ✓ | | |

VENDEDOR e ESTOQUISTA veem, no lugar do dashboard, uma página inicial com atalhos para as telas do seu perfil.
Usuário desativado ou com perfil alterado perde o acesso na hora, mesmo com o token ainda válido.

## Como rodar localmente

### Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) 22.12 ou superior (recomendado: 24 LTS)
- [Docker Desktop](https://www.docker.com/) (no Windows, com WSL 2), para o MySQL
- Git

### Endereços em desenvolvimento

| Serviço | Endereço |
| --- | --- |
| MySQL (Docker) | `localhost:3307` |
| Backend (API) | http://localhost:5080 |
| Swagger UI | http://localhost:5080/swagger |
| Health check | http://localhost:5080/health |
| Frontend | http://localhost:5173 |

O MySQL do Docker usa a porta **3307** para não conflitar com um MySQL instalado direto na máquina (no Windows, o
serviço `MySQL80` costuma ocupar a 3306). Não é preciso parar nem alterar esse serviço.

### Passo a passo (Windows / PowerShell)

```powershell
# 1. Código
git clone https://github.com/gugushow/adega-system-tcc.git
cd adega-system-tcc

# 2. Banco de dados (MySQL 9.7 no Docker, porta 3307)
Copy-Item .env.example .env
docker compose up -d

# 3. Secrets da API (ver a seção "User Secrets" logo abaixo)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3307;Database=adega;User=adega;Password=SUA_SENHA;" --project backend/src/AdegaSystem.Api
dotnet user-secrets set "Jwt:Secret" "SUA_CHAVE_JWT" --project backend/src/AdegaSystem.Api
dotnet user-secrets set "Admin:InitialPassword" "SUA_SENHA_DE_ADMIN" --project backend/src/AdegaSystem.Api

# 4. Tabelas do banco (migrations)
dotnet tool restore
dotnet ef database update --project backend/src/AdegaSystem.Api

# 5. Backend (deixe este terminal aberto)
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http
```

Em outro terminal:

```powershell
# 6. Frontend
cd frontend
npm install
npm run dev
```

No Linux ou macOS os comandos são os mesmos, trocando `Copy-Item .env.example .env` por `cp .env.example .env`.

### User Secrets

A API lê três configurações sensíveis dos [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
do .NET, que ficam no seu perfil de usuário, fora do repositório. Nenhum arquivo versionado contém esses valores.

| Chave | O que colocar |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | `Server=localhost;Port=3307;Database=adega;User=adega;Password=SUA_SENHA;`, trocando `SUA_SENHA` pelo `MYSQL_PASSWORD` do seu `.env` |
| `Jwt:Secret` | Uma chave aleatória com **32 caracteres ou mais** (`SUA_CHAVE_JWT`). Sem ela, ou mais curta, a API não sobe |
| `Admin:InitialPassword` | A senha do usuário `admin@adega.local` na primeira subida (8+ caracteres, com letras e números) |

- Para gerar a chave JWT no PowerShell: `[guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')` (64 caracteres).
- Quem tem a chave JWT consegue criar tokens de qualquer perfil: ela nunca vai para o repositório.
- `dotnet user-secrets list --project backend/src/AdegaSystem.Api` mostra o que está configurado.
- Fora do desenvolvimento, use as variáveis de ambiente `ConnectionStrings__DefaultConnection`, `Jwt__Secret` e `Admin__InitialPassword`.

### Migrations

`dotnet ef database update --project backend/src/AdegaSystem.Api` cria ou atualiza as tabelas. Rode de novo sempre que
uma atualização trouxer migration nova, **com a API parada** (com ela rodando, o build falha com o arquivo
`AdegaSystem.Api.exe` travado, erro MSB3027).

Depois de mudar alguma entidade, gere uma nova migration:

```powershell
dotnet ef migrations add NomeDaMigration --project backend/src/AdegaSystem.Api --output-dir Data/Migrations
```

### Primeiro acesso

1. Abra http://localhost:5173.
2. Entre com o e-mail `admin@adega.local` e a senha que você definiu em `Admin:InitialPassword`.
3. Troque a senha em **Alterar minha senha** (menu do usuário, no canto superior direito).
4. Em **Usuários**, crie as contas de VENDEDOR e ESTOQUISTA.

A senha inicial só é usada enquanto o admin ainda não tem senha. Depois disso, mudar o User Secret não altera nada.

Para testar a API pelo Swagger (http://localhost:5080/swagger, só em desenvolvimento), faça login em
`POST /api/auth/login`, clique em **Authorize** e cole o token recebido.

### Dados de demonstração (opcional)

Para navegar por uma adega "em operação" (produtos, lotes vencendo, compras, vendas dos últimos 30 dias, combos e
usuários de todos os perfis), carregue os dados fictícios num banco **separado**, o `adega_demo`:

```powershell
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http -- seed-demo   # carrega e encerra
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http -- use-demo    # sobe a API no banco demo
```

Só funciona em Development, com a connection string própria `ConnectionStrings:DemoConnection` e a senha dos
usuários demo em `DemoData:Password` (User Secrets). O banco `adega` normal nunca é tocado. Preparação, logins,
cenários e reset em [docs/DADOS-DEMO.md](docs/DADOS-DEMO.md).

**Só com Docker (para apresentações):** o `docker-compose.demo.yml` sobe MySQL, carga demo, API e frontend em
`http://localhost:8081`, separado da produção e sem precisar de internet depois do primeiro build. Veja
[docs/DEMO-DOCKER.md](docs/DEMO-DOCKER.md).

## Testes e verificação

```powershell
dotnet build                 # compila a solution
dotnet test                  # testes do backend

cd frontend
npm run lint                 # Oxlint
npm run build                # checagem de tipos (tsc) + build de produção
npm test                     # testes do frontend (Vitest)
```

Os testes do backend rodam os services contra um SQLite em memória, sem precisar do MySQL. Os testes de permissão e
de comportamento da API (`Auth/`, `Http/`) sobem a API inteira em memória (`WebApplicationFactory`), fazem login e
chamam os endpoints por HTTP.

Resultado na preparação da v1.0.0: **341 testes, 341 aprovados, 0 falhas**; `dotnet build` com 0 avisos e 0 erros;
`npm run lint` e `npm run build` sem erros. Na v1.0.0 o frontend não tinha testes automatizados: foi validado no navegador.

Os testes do frontend (`npm test`) usam Vitest, Testing Library e jsdom, com a câmera e a API simuladas. Começaram
com o leitor de código de barras (componente do leitor, formulário de produto, tela de Produtos e PDV).

## Deploy (produção com Docker)

O `docker-compose.prod.yml` sobe o sistema inteiro num servidor com Docker: MySQL, API e frontend.

```
navegador ──► web (nginx, porta 8080) ──► /api, /health ──► api (ASP.NET, porta 8080 interna) ──► mysql (sem porta exposta)
                    └── demais rotas: arquivos do React (index.html)
```

1. Copie o modelo e preencha os valores (o `.env.prod` não vai para o Git):

   ```bash
   cp .env.prod.example .env.prod
   ```

   As senhas e a chave JWT vêm **vazias** no modelo: enquanto não forem preenchidas, o `docker compose` se recusa a
   subir e avisa qual variável falta.

   | Variável | O que é |
   | --- | --- |
   | `MYSQL_ROOT_PASSWORD`, `MYSQL_PASSWORD` | Senhas do banco (use letras e números) |
   | `MYSQL_DATABASE`, `MYSQL_USER` | Nome do banco e usuário da API |
   | `JWT_SECRET` | Chave dos tokens, 32+ caracteres (`openssl rand -hex 32`) |
   | `ADMIN_INITIAL_PASSWORD` | Senha do `admin@adega.local` na primeira subida (8+ caracteres, letras e números) |
   | `APP_PORT` | Porta do sistema na máquina (padrão 8080) |

2. Suba (a primeira vez demora, porque compila as imagens):

   ```bash
   docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
   ```

3. Acesse `http://servidor:8080`, entre com `admin@adega.local` e a senha de `ADMIN_INITIAL_PASSWORD`, e troque a senha
   em "Alterar minha senha". `http://servidor:8080/health` responde `Healthy` quando a API e o banco estão no ar.

O que acontece por baixo:

- A API aplica as migrations sozinha ao subir (`Database__MigrateOnStartup=true`), então banco novo e atualizações não precisam do `dotnet ef`.
- Swagger e OpenAPI só existem em desenvolvimento; em produção a API não publica a documentação.
- O frontend chama a API pelo mesmo endereço (`/api`), por isso não há configuração de CORS em produção.
- O nginx esconde a versão (`server_tokens off`), limita o tamanho das requisições e envia os cabeçalhos `X-Content-Type-Options`, `X-Frame-Options` e `Referrer-Policy`.
- A API roda com usuário sem privilégios dentro do contêiner, e o MySQL não fica exposto fora do Docker.
- A imagem da API usa a base padrão do .NET (Ubuntu), não a Alpine: a Alpine não tem o fuso de Brasília, e as vendas cairiam no dia errado.

**Atualizar para uma versão nova:** `git pull` e o mesmo comando do passo 2. Os dados ficam no volume `adega-prod_mysql-data`.

**Backup do banco:**

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod exec mysql sh -c 'mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" adega' > backup.sql
```

**HTTPS:** coloque um proxy com certificado na frente da porta 8080 (Caddy, Traefik, nginx com Let's Encrypt, ou um
túnel como Cloudflare). A API já entende os cabeçalhos `X-Forwarded-*` e envia HSTS quando o acesso é por HTTPS.
Sem HTTPS, use só em rede interna.

## Documentação

| Documento | Conteúdo |
| --- | --- |
| [docs/API.md](docs/API.md) | Todos os endpoints, códigos de resposta e filtros |
| [docs/DADOS-DEMO.md](docs/DADOS-DEMO.md) | Dados de demonstração no banco `adega_demo`: como carregar, o que tem, como foi feito e proteções |
| [docs/DEMO-DOCKER.md](docs/DEMO-DOCKER.md) | Demonstração completa só com Docker (`docker-compose.demo.yml`): preparar, usar, reset e uso sem internet |
| [docs/LEITOR-CODIGO-DE-BARRAS.md](docs/LEITOR-CODIGO-DE-BARRAS.md) | Leitor pela câmera: biblioteca, UPC-A × EAN-13, HTTPS, privacidade e roteiro de teste manual |
| [docs/RELEASE-v1.0.0.md](docs/RELEASE-v1.0.0.md) | Notas da release: módulos, decisões técnicas, segurança, testes e limitações |
| [CHANGELOG.md](CHANGELOG.md) | Histórico de versões |
| [docs/PLANO.md](docs/PLANO.md) | Plano do projeto: arquitetura, entidades e fases |
| Guias de estudo | [Fase 1](docs/FASE-1.md) · [Fase 2](docs/FASE-2.md) · [Fase 3](docs/FASE-3.md) · [Fase 4](docs/FASE-4.md) · [Fase 5](docs/FASE-5.md) · [Fase 6](docs/FASE-6.md) · [Fase 7](docs/FASE-7.md) · [Fase 8](docs/FASE-8.md) · [Etapa de UI/UX](docs/ETAPA-UI-UX.md) |

## Limitações conhecidas

- Trocar a senha não derruba sessões abertas em outros computadores: elas continuam até o token vencer (no máximo 8 horas). Desativar o usuário ou mudar o perfil derruba na hora.
- Listas não ordenam clicando no cabeçalho da coluna, e os relatórios não exportam para PDF/Excel.
- Os filtros de fornecedor e de usuário carregam até 100 itens (suficiente para uma adega).
- Não há emissão de cupom ou nota fiscal: o sistema é gerencial, não fiscal.
- O aviso "Storing keys in a directory ... may not be persisted" no log da API em Docker pode ser ignorado: o sistema não usa esse recurso (o login é por JWT).

## Roadmap (depois da v1.0.0)

Ideias para as próximas versões, ainda não implementadas:

- Impressão de comprovante da venda (cupom não fiscal) no PDV.
- Leitor de código de barras pela câmera também na tela de compra, e leitura contínua no PDV (vários produtos sem reabrir o leitor).
- Exportação de relatórios para PDF e Excel.
- Deploy real em servidor com HTTPS e backup automático do banco.
- Notificações de estoque baixo e de lotes perto do vencimento (e-mail ou WhatsApp).
- Melhorias no PDV: divisão do pagamento em mais de uma forma e troco calculado.
- Novos relatórios: curva ABC de produtos, vendas por vendedor e por horário.
- Integração fiscal (NFC-e) com um provedor homologado.

## Comandos úteis

| Comando | Descrição |
| --- | --- |
| `docker compose up -d` | Sobe o MySQL de desenvolvimento (porta 3307) |
| `dotnet build` | Compila a solution |
| `dotnet test` | Roda os testes do backend |
| `dotnet ef database update --project backend/src/AdegaSystem.Api` | Aplica as migrations |
| `dotnet run --project backend/src/AdegaSystem.Api --launch-profile http` | Sobe a API em http://localhost:5080 |
| `npm run dev` (em `frontend/`) | Sobe o frontend em http://localhost:5173 |
| `npm run lint` (em `frontend/`) | Lint com Oxlint |
| `npm run build` (em `frontend/`) | Checa tipos e gera o build de produção |
| `npm test` (em `frontend/`) | Roda os testes do frontend (Vitest) |
