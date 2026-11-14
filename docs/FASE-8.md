# Fase 8: Refinamento, Testes, Documentação e Deploy (guia de estudo)

Este guia acompanha o PR da Fase 8, a última do plano. Nenhuma regra de negócio validada mudou. A fase faz quatro coisas:

1. **Telas**: acabamento visual e pequenos ganhos de usabilidade.
2. **API**: pontos de robustez e segurança que só aparecem com uso real (duas pessoas ao mesmo tempo, valores absurdos, tentativas de senha).
3. **Testes**: de 271 para 341, cobrindo o caminho HTTP e os casos que faltavam.
4. **Deploy**: imagens Docker, um compose de produção e o README final.

A lista completa do que foi revisado, e do que ficou de fora, está em `REVISAO-FASE-8.md` (na pasta do projeto).

---

## 1. Telas

### Cor da marca: `src/styles/theme.css`

O menu e o login já eram vinho, mas os botões eram o azul padrão do Bootstrap. O Bootstrap 5.3 usa **variáveis CSS**
(`--bs-primary`, `--bs-btn-bg`...). Em vez de recompilar o Bootstrap, o `theme.css` só redefine essas variáveis depois
de importar o Bootstrap (ver `main.tsx`):

```css
:root { --adega-wine: #7b1e3a; --bs-primary: var(--adega-wine); ... }
.btn-primary { --bs-btn-bg: var(--adega-wine); --bs-btn-hover-bg: var(--adega-wine-hover); ... }
```

Os gráficos **não** mudaram de cor: o azul e o laranja deles foram escolhidos para quem tem daltonismo distinguir as duas séries.

### Título da aba: `hooks/usePageTitle.ts`

Cada tela vira "Produtos · Adega System". O `AdminLayout` descobre o título pela rota atual (o mesmo texto do menu),
então nenhuma página precisou mudar. Com várias abas abertas, dá para saber qual é qual.

### Página não encontrada: `pages/NotFoundPage.tsx`

Antes, um endereço errado voltava para o início sem explicar. Agora a rota `*` do `App.tsx` mostra "Página não encontrada"
com um link de volta.

### O "undefined" no diálogo de confirmação

Ao fechar o diálogo de "Desativar", aparecia por um instante `Deseja desativar "undefined"?`. O motivo: a página zera
o item (`setToDeactivate(null)`) **antes** da animação de fechar terminar, e a mensagem é montada com o item já nulo.

A correção em `ConfirmDialog.tsx` guarda o último título e mensagem enquanto o diálogo está aberto:

```tsx
const [shown, setShown] = useState({ title, message })
if (show && (shown.title !== title || shown.message !== message)) {
  setShown({ title, message })   // só atualiza enquanto está aberto
}
```

Atualizar estado durante a renderização parece estranho, mas é o padrão recomendado pelo React para "derivar estado de
props" (sem `useEffect`, que mostraria um quadro errado antes de corrigir).

### Formulários que não fecham sem querer

Clicar fora do modal ou apertar Esc fechava o formulário e perdia o que foi digitado. Os modais de cadastro agora usam:

```tsx
<Modal show onHide={saving ? undefined : onClose} backdrop="static" keyboard={false}>
```

- `backdrop="static"`: clicar fora não fecha.
- `keyboard={false}`: Esc não fecha.
- Fecham pelo **Cancelar** ou pelo **X**. Diálogos curtos (confirmações, detalhes) continuam fechando normalmente.

Nas buscas com sugestões (`ProductPicker`, `CustomerPicker`), Esc fecha só a lista: `event.stopPropagation()` impede que o
Esc chegue ao modal.

### Filtros de data: `components/DateInput.tsx`

As duas caixas de data eram iguais. Agora cada uma tem o rótulo grudado ("De", "Até") e `min`/`max` impedem um "De" depois do "Até":

```tsx
<DateInput label="De" value={from} max={to} onChange={...} />
<DateInput label="Até" value={to} min={from} onChange={...} />
```

### Links que já chegam filtrados

Os alertas do dashboard abrem a lista certa: "Estoque baixo" vai para `/produtos?estoque=Low`, "Vencidos" para
`/lotes?visao=expired`. Cada página lê o parâmetro só para o **valor inicial** do filtro:

```tsx
const [searchParams] = useSearchParams()
const [stock, setStock] = useState(() => {
  const value = searchParams.get('estoque')
  return value === 'Low' || value === 'Out' ? value : ''
})
```

O valor é conferido contra a lista de opções válidas: um `?estoque=qualquercoisa` vira "todos".

### Sessão expirada volta para a mesma tela

Quando o token vence, o `api/http.ts` manda para `/login?sessao=expirada&de=/compras/nova`. Depois do login, o
`LoginPage` volta para o endereço de `de`, mas só se ele começar com `/` (e não `//`): assim ninguém monta um link que,
depois do login, leva para outro site.

### PDV

- **F2** volta para a busca, **F9** finaliza a venda. O atalho fica desligado com um modal aberto (cadastro de cliente,
  detalhes da venda), para não finalizar sem querer. A dica "(F9)" só aparece em tela grande: celular não tem teclado físico.
- Erro ao carregar os produtos aparece em cima da lista (antes aparecia dentro do carrinho e não sumia).
- O carrinho virou uma lista flexível em vez de tabela: no celular, nome e controles quebram de linha sem espremer.
- O "−" fica desabilitado na quantidade 1 (para remover existe o botão de remover).

### Formatos iguais em todo lugar: `utils/format.ts`

- `formatUnitCost`: custo unitário com até 4 casas (a coluna do banco é `decimal(12,4)`). Antes, cada tela tinha a sua função.
- `formatNumber`: separador de milhar (`1.250`) também nos relatórios.

### Acessibilidade

Botões de fechar e a paginação eram anunciados em inglês por leitores de tela ("Close", "Previous"). Agora: `closeLabel="Fechar"`
nos modais e alertas, e "Página anterior"/"Próxima página" na paginação (texto só para leitor de tela, com a classe `visually-hidden`).

---

## 2. API

### Só o ADMIN muda a situação do cliente

A regra aprovada na Fase 5 é que só o ADMIN desativa clientes. A rota `DELETE` já exigia ADMIN, mas o `PUT` (que o vendedor
usa para editar) gravava o campo `Active` que viesse na requisição. Ou seja, o vendedor desativava pela edição.

```csharp
// CustomersController
await customerService.UpdateAsync(id, dto, canChangeStatus: User.IsInRole(Roles.Admin))

// CustomerService
if (canChangeStatus) { customer.Active = dto.Active; }
```

O controller sabe **quem** está chamando (o token); o service recebe só a decisão. A tela também esconde o botão "Ativo" para o vendedor,
mas quem garante a regra é a API: alguém chamando a API direto também é barrado.

### Duas pessoas salvando o mesmo CPF ao mesmo tempo: 409

Os services conferem duplicidade antes de gravar (`EnsureCpfIsAvailableAsync`). Mas entre conferir e gravar existe um instante: se duas
pessoas salvam o mesmo CPF juntas, as duas passam na conferência. Quem barra a segunda é o **índice único** do banco, com o erro
MySQL 1062. Antes isso virava 500 ("erro inesperado"); agora o `GlobalExceptionHandler` reconhece o código e responde 409:

```csharp
_ when mySqlError == MySqlDuplicateEntry => (StatusCodes.Status409Conflict, ...)
```

`FindMySqlErrorNumber` procura o `MySqlException` dentro da exceção, porque o EF embrulha o erro do banco num `DbUpdateException`.

### Dois resgates de pontos ao mesmo tempo

O saldo de pontos é a soma do extrato. O resgate faz: lê o saldo → confere → grava. Se dois resgates do mesmo cliente rodam juntos, os
dois leem o mesmo saldo e os dois passam, e o cliente gasta os pontos duas vezes.

A correção é uma transação **Serializable** no resgate e no ajuste:

```csharp
await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
var balance = await GetBalanceAsync(customerId);
...
await transaction.CommitAsync();
```

No nível Serializable, o MySQL trava as linhas do extrato que a soma leu. O segundo resgate não consegue gravar enquanto o primeiro não
termina; quando os dois disputam, o MySQL desfaz um deles (deadlock, erro 1213) e a API responde 409 "tente novamente". O saldo nunca fica errado.

Por que não usar o mesmo token de concorrência do estoque (`IsConcurrencyToken`)? Ele protege **uma linha** (o `CurrentStock` do produto).
O saldo de pontos não é uma linha, é uma soma; não há onde pôr o token sem criar uma coluna nova.

### Limite de quantidade: `Validators/QuantityRules.cs`

Uma compra com 3 bilhões de unidades estourava o `int` nas contas e virava 500. Agora toda quantidade (compra, venda, combo, ajuste) e
todo valor de pontos aceitam no máximo **100.000**, com a mensagem "A quantidade deve ser de no máximo 100.000.". É bem acima de qualquer
uso real; existe só para transformar um valor absurdo numa mensagem clara.

### Limite de tentativas de login: `Auth/LoginRateLimit.cs`

Sem limite, um programa pode testar milhares de senhas por minuto. O ASP.NET já traz um limitador (`AddRateLimiter`):

```csharp
options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
    httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
```

- **Partição por IP**: cada endereço tem as suas 10 tentativas por minuto.
- Só o login usa a política (`[EnableRateLimiting("login")]` no `AuthController`); o resto da API não é afetado.
- A 11ª tentativa recebe **429** no formato padrão: "Muitas tentativas de login. Aguarde um minuto e tente novamente."
- Atrás do nginx, o IP da conexão seria sempre o do nginx. Por isso o compose de produção liga `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`:
  o ASP.NET passa a ler o IP real do cabeçalho `X-Forwarded-For` que o nginx envia.

### Outros acertos

| O quê | Antes | Agora |
| --- | --- | --- |
| Senha inicial do admin | Aceitava qualquer valor | Segue a regra de senha (`PasswordRules.IsStrong`); se for fraca, não grava e avisa no log |
| Filtro inválido na URL (`?page=abc`) | 400 sem dizer o campo | 400 com `errors: [{ field: "page" }]` |
| Cliente que fecha a aba no meio da requisição | Erro 500 no log | Registro informativo; não é erro do sistema |
| Descrição só com espaços | Gravava `""` em alguns cadastros | Sempre `null` (`Common/Text.cs`, `EmptyToNull`) |
| Cadastro de usuário e combo | Dois `SaveChanges` soltos | Mesma transação: o registro e a auditoria são gravados juntos ou nenhum |
| Comandos SQL no log | Desligados só em desenvolvimento | Desligados em todos os ambientes (`appsettings.json`) |

---

## 3. Testes

São 341 testes (eram 271). Os novos se dividem em três grupos.

### Pelo caminho HTTP: `Http/ApiBehaviorTests.cs`

Sobem a API inteira em memória (`ApiFactory`, a mesma dos testes de permissão) e conferem o que o frontend recebe:

- `POST` responde **201** com o cabeçalho `Location` apontando para o registro criado.
- Erro de validação vem com o nome do campo como no JSON (`name`, não `Name`).
- JSON malformado e filtro inválido na URL respondem 400 com mensagem amigável.
- `pageSize=1000` vira 100.
- 404 no formato padrão; token vencido recusado.
- Vendedor edita cliente mas não desativa; ADMIN desativa.
- A 4ª tentativa de login com limite 3 recebe 429; o limite não afeta as outras rotas.

A base comum (login, cliente HTTP com token, leitura do corpo) foi para `Infrastructure/ApiTestBase.cs`, usada também pelos testes de permissão.

**Limite de login nos testes:** os testes fazem dezenas de logins seguidos do mesmo "endereço". Por isso o `ApiFactory` usa um limite alto
(`LoginPermitLimit = 10_000`), e o teste do limite cria uma fábrica própria com `LoginPermitLimit = 3`.

### Tratamento de erros: `Middleware/GlobalExceptionHandlerTests.cs`

Erros do MySQL (1062, 1213) não acontecem no SQLite dos testes. O teste chama o handler direto com uma exceção montada:

```csharp
// O MySqlException não tem construtor público: o teste cria um por reflexão, com o código de erro desejado.
var mySql = (MySqlException)Activator.CreateInstance(typeof(MySqlException),
    BindingFlags.NonPublic | BindingFlags.Instance, null, ["erro do banco", number], null)!;
```

E confere: duplicado e deadlock viram 409; outro erro de banco vira 500 e vai para o log; cancelamento pelo cliente não é registrado como erro.

### Regras que faltavam

- Edição com nome/CPF/CNPJ de outro registro é recusada (categorias, fornecedores, clientes).
- Compras: nota com espaços conta como repetida, a mesma nota de outro fornecedor é aceita, produto inexistente não grava nada, custo com 5 casas é recusado.
- Relatórios: "só abaixo da margem mínima"; produto nunca vendido aparece nos parados.
- Baixa de lote: motivo padrão e motivo longo demais.
- Usuário não se desativa pela edição.
- CPF (`Common/CpfTests.cs`), senha inicial do admin (inclusive senha fraca) e o limite de 100.000 em todos os validadores.

---

## 4. Deploy

### A ideia geral

```
navegador ──► web (nginx :80) ──► /api ──► api (ASP.NET :8080) ──► mysql (:3306)
                 └── outras rotas: index.html do React
```

- **Só o nginx tem porta publicada.** A API e o MySQL ficam na rede interna do Docker. Ninguém de fora chega ao banco.
- **Mesmo endereço para tela e API.** O frontend chama `/api` (o padrão do `api/http.ts`), o nginx repassa para a API. Sem domínios diferentes, não há CORS.

### `backend/src/AdegaSystem.Api/Dockerfile`

Duas etapas (**multi-stage build**):

1. `sdk:10.0` restaura os pacotes e publica em Release. O `.csproj` é copiado antes do resto do código: enquanto os pacotes não mudam, o
   Docker reaproveita o `dotnet restore` do cache e o build fica rápido.
2. `aspnet:10.0` recebe só a pasta publicada. A imagem final não tem SDK nem código-fonte.

Dois detalhes:

- **Não é a imagem `-alpine`.** A Alpine não traz o banco de fusos horários (`tzdata`). Sem ele, `America/Sao_Paulo` não existe e o
  `BusinessClock` não consegue dizer qual é o "hoje" de Brasília: vendas, relatórios e validades cairiam no dia errado. A imagem padrão
  do .NET 10 (Ubuntu) já traz o `tzdata`.
- `USER $APP_UID`: a API roda com um usuário sem privilégios, que a própria imagem já cria.

### `frontend/Dockerfile` e `nginx.conf`

1. `node:24-alpine` roda `npm ci` e `npm run build` (gera a pasta `dist`).
2. `nginx:1.30-alpine` serve a `dist`. Aqui a Alpine não tem problema: o nginx não precisa de fuso.

No `nginx.conf`:

- `try_files $uri $uri/ /index.html`: `/produtos` não é um arquivo; o nginx devolve o `index.html` e o React Router mostra a tela.
- `/assets/` com cache de 1 ano: os arquivos do Vite têm um código no nome (`index-1KdJtDwW.js`), e cada versão nova gera nomes novos.
  O `index.html` fica com `no-cache`, para a versão nova chegar na hora.
- `X-Forwarded-For` e `X-Forwarded-Proto` informam à API o IP real do cliente e se o acesso original foi por HTTPS.
- Cabeçalhos de segurança básicos (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`).

### `docker-compose.prod.yml`

- O MySQL **não tem `ports`**.
- A API espera o MySQL ficar saudável (`depends_on: condition: service_healthy`).
- Toda configuração vem do `.env.prod`, que não vai para o Git (o `.gitignore` agora ignora `.env.*`, menos os `.example`).
- `name: adega-prod` separa este ambiente do banco de desenvolvimento: dá para rodar os dois na mesma máquina.

### Migrations ao subir: `Database:MigrateOnStartup`

No seu PC, as migrations continuam com `dotnet ef database update`. Num servidor não há SDK, então a API pode aplicá-las sozinha:

```csharp
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AdegaDbContext>().Database.MigrateAsync();
}
```

Fica **desligado por padrão** (`appsettings.json`) e ligado só no compose de produção. Roda antes do `AdminPasswordInitializer`, que
precisa da tabela de usuários pronta.

### HTTPS

A API não tem certificado: ela fala HTTP dentro do Docker. O HTTPS fica num proxy na frente (Caddy, Traefik, Cloudflare...). Por isso:

- Saiu o `UseHttpsRedirection` (dentro do Docker não há porta HTTPS para onde redirecionar; ele só gerava um aviso no log).
- Ficou o `UseHsts`: quando o acesso original foi por HTTPS (o proxy avisa em `X-Forwarded-Proto`), a API manda o cabeçalho HSTS e o
  navegador passa a recusar HTTP naquele site.

### Testado aqui

As duas imagens foram compiladas e o compose de produção subiu do zero: as 6 migrations aplicadas, a senha do admin definida pela variável,
login pelo nginx, rotas do React (`/produtos` direto no navegador), cabeçalhos de cache e o limite de login (a 11ª tentativa recebeu 429).

---

## Como testar no seu PC

1. Atualize a main e rode os testes: `dotnet test` (341 testes).
2. Não há migration nova nesta fase. Rode a API e o frontend como sempre e confira:
   - Botões vinho, título da aba, `/qualquer-coisa` mostra "Página não encontrada".
   - Dashboard: clique em "Estoque baixo" e em "Ver lotes".
   - Feche um diálogo de desativar e veja que não aparece "undefined".
   - Num cadastro, clique fora do modal: ele não fecha.
   - PDV: F2 e F9.
   - Entre como VENDEDOR, edite um cliente: não há o botão "Ativo".
   - Erre a senha 11 vezes seguidas no login: aparece "Muitas tentativas...".
3. Opcional, produção local (não mexe no banco de desenvolvimento nem na porta 3306/3307):
   ```bash
   cp .env.prod.example .env.prod   # troque os valores
   docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
   ```
   Acesse http://localhost:8080. Para desligar e apagar esse banco de teste:
   `docker compose -f docker-compose.prod.yml --env-file .env.prod down -v`.
