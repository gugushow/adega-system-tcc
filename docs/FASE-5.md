# Fase 5: Login, JWT, Perfis e Usuários (guia de estudo)

Este guia acompanha o PR da Fase 5. Até a Fase 4, a API estava aberta: qualquer um que soubesse o endereço podia cadastrar produtos, registrar compras ou cancelar vendas, e tudo ficava registrado em nome do admin semeado. Agora cada pessoa entra com o próprio e-mail e senha, recebe um **token JWT**, e a API decide o que ela pode fazer pelo **perfil** (ADMIN, VENDEDOR ou ESTOQUISTA).

Dashboard e relatórios continuam fora (Fase 6).

---

## 1. Dois conceitos que não se misturam

| | Autenticação | Autorização |
|---|---|---|
| Pergunta | **Quem** é você? | O que você **pode** fazer? |
| Onde acontece | Login (confere e-mail e senha) e, depois, a leitura do token em cada requisição | `[Authorize(Roles = ...)]` nos controllers |
| Quando falha | **401 Unauthorized**: "Sua sessão expirou ou você não fez login." | **403 Forbidden**: "Você não tem permissão para acessar este recurso." |
| No pipeline | `app.UseAuthentication()` | `app.UseAuthorization()` |

A ordem no `Program.cs` importa: primeiro descobre quem é o usuário, depois decide se ele pode entrar.

---

## 2. O que é um JWT

JWT (JSON Web Token) é um texto em três partes separadas por ponto:

```
eyJhbGciOiJIUzI1NiJ9 . eyJzdWIiOiIxIiwicm9sZSI6IkFETUlOIiwiZXhwIjoxNzk...} . 3q2-7Fh0kQ...
      cabeçalho                      conteúdo (claims)                           assinatura
```

- **Cabeçalho**: o algoritmo da assinatura (`HS256` = HMAC-SHA256).
- **Conteúdo (claims)**: os dados do usuário. Usamos `sub` (id), `name`, `email` e `role`, mais `exp` (quando vence), `iss` (quem emitiu) e `aud` (para quem).
- **Assinatura**: um "lacre" calculado com o conteúdo + o **Secret**, que só a API conhece.

O conteúdo é só Base64: **qualquer um consegue ler** (cole um token em jwt.io e veja). Por isso nunca colocamos senha nem dado sensível nele. O que impede a falsificação é a assinatura: se alguém trocar `"role":"VENDEDOR"` por `"role":"ADMIN"`, a assinatura não bate mais e a API recusa o token.

**Por que token e não sessão no servidor?** O token carrega tudo o que a API precisa para saber quem é o usuário, então a API não guarda estado entre requisições. É o padrão para um frontend React separado do backend.

Arquivo: `Services/TokenService.cs` cria o token com `JsonWebTokenHandler`, a biblioteca atual da Microsoft para JWT.

---

## 3. Senha: só o hash

A senha **nunca** é gravada. O que vai para `User.PasswordHash` é um hash gerado pelo `PasswordHasher<User>` do próprio ASP.NET Core:

- usa PBKDF2 com HMAC-SHA512 e milhares de iterações (lento de propósito, para dificultar ataques de força bruta);
- usa um **salt** aleatório por senha: duas pessoas com a mesma senha têm hashes diferentes;
- o hash guarda a versão do algoritmo. Se um dia o .NET recomendar um algoritmo mais forte, o login devolve `SuccessRehashNeeded` e o `AuthService` grava o hash novo na hora (é o único momento em que temos a senha em mãos).

No login, o `VerifyHashedPassword` refaz a conta com a senha digitada e compara com o hash salvo.

**Regra de senha** (`Validators/UserValidators.cs` → `PasswordRules`): pelo menos 8 caracteres, com pelo menos uma letra e um número.

---

## 4. Segredos: nada no repositório

| Configuração | Onde fica | Por quê |
|---|---|---|
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationMinutes` | `appsettings.json` | Não são segredo |
| `Jwt:Secret` | **User Secrets** (dev) ou variável `Jwt__Secret` (produção) | Quem tem o Secret consegue criar tokens de ADMIN |
| `Admin:InitialPassword` | **User Secrets** (dev) ou variável `Admin__InitialPassword` | Senha do primeiro login |
| `ConnectionStrings:DefaultConnection` | User Secrets (desde a Fase 1) | Senha do banco |

### O Secret do JWT

O HMAC-SHA256 exige uma chave de pelo menos 256 bits (32 bytes). O `AuthenticationSetup` confere isso **na subida da API** (`ValidateOnStart`): sem Secret, ou com menos de 32 caracteres, a API nem sobe e mostra o comando para configurar. Sem essa conferência, ela subiria normalmente e só falharia no primeiro login, um erro bem mais difícil de entender.

### A senha do admin semeado

O admin criado pela migration (`admin@adega.local`) tem `PasswordHash` vazio de propósito: uma senha na migration iria para o repositório. Quando a API sobe, o `AdminPasswordInitializer`:

1. procura o admin (Id 1);
2. se ele **ainda não tem senha** e existe `Admin:InitialPassword`, grava o hash dessa senha;
3. se não existe a configuração, escreve um aviso no log com o comando para configurar.

Depois que o admin tem senha, o initializer não faz mais nada: mudar `Admin:InitialPassword` não troca a senha. Para trocar, use **Alterar minha senha** na tela.

Enquanto o hash estiver vazio, ninguém consegue entrar como admin (o login recusa hash vazio).

---

## 5. O fluxo do login (POST /api/auth/login)

```
AuthController.Login   [AllowAnonymous]
  ↓ LoginDto validado (e-mail e senha preenchidos)
AuthService.LoginAsync
  ↓ e-mail normalizado: trim + minúsculas ("  Ana@Adega.com " = "ana@adega.com")
  ↓ usuário existe? está ativo? tem senha? a senha confere?
  │    qualquer "não" → UnauthorizedException("E-mail ou senha inválidos.") → 401
  ↓ TokenService.CreateToken(user) → token + quando vence
  ↓ 200 { token, expiresAt, user }
```

**Por que a mesma mensagem para tudo?** Se a API dissesse "e-mail não cadastrado" num caso e "senha errada" no outro, alguém poderia descobrir quais e-mails existem só tentando logar.

---

## 6. O caminho de uma requisição protegida

```
GET /api/purchases   Authorization: Bearer eyJ...
  ↓ UseAuthentication (JwtBearer)
  │    assinatura confere com o Secret?  Issuer e Audience certos?  ainda não venceu?
  │    OnTokenValidated: no banco, o usuário continua ativo e com o mesmo perfil do token?
  │    qualquer "não" → OnChallenge → 401 { "Sua sessão expirou ou você não fez login..." }
  ↓ UseAuthorization
  │    o endpoint tem [AllowAnonymous]? → passa
  │    tem [Authorize(Roles = "ADMIN,ESTOQUISTA")]? o perfil do token está na lista?
  │    não tem atributo nenhum? → FallbackPolicy: basta estar logado
  │    sem permissão → OnForbidden → 403 { "Você não tem permissão..." }
  ↓ PurchasesController.GetAll
```

Tudo isso está em `Auth/AuthenticationSetup.cs`, para o `Program.cs` continuar curto.

### Fechado por padrão (FallbackPolicy)

```csharp
options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
```

Todo endpoint exige login, a não ser que diga o contrário com `[AllowAnonymous]` (o login) ou `.AllowAnonymous()` (`/health` e o documento OpenAPI). A vantagem: um controller novo nunca fica aberto porque alguém esqueceu um `[Authorize]`.

### Por que conferir o banco a cada requisição?

O token vale 8 horas. Se o ADMIN desativar um funcionário às 10h, o token dele continuaria valendo até as 18h. O `OnTokenValidated` faz uma consulta pequena (só `Active` e `Role` pela chave primária) e derruba o token na hora se o usuário foi desativado ou mudou de perfil. É um custo pequeno para um sistema de loja, e evita ter que criar uma "lista negra" de tokens.

### Detalhes da validação

- `ClockSkew = TimeSpan.Zero`: por padrão o .NET aceita o token até 5 minutos depois de vencido. Aqui vence na hora certa.
- `MapInboundClaims = false`: mantém os nomes curtos (`sub`, `role`). Sem isso, o .NET troca `role` por `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`.
- `RoleClaimType = "role"`: diz ao `[Authorize(Roles = ...)]` em qual claim procurar o perfil.

---

## 7. Quem pode o quê

| Área | ADMIN | VENDEDOR | ESTOQUISTA |
|---|:-:|:-:|:-:|
| Login, `/auth/me`, trocar a própria senha | ✓ | ✓ | ✓ |
| Produtos e categorias: consultar | ✓ | ✓ | ✓ |
| Produtos e categorias: cadastrar, editar, desativar | ✓ | | ✓ |
| Estoque, lotes, compras, fornecedores, histórico de custo | ✓ | | ✓ |
| Vendas (PDV e histórico) e clientes | ✓ | ✓ | |
| Cancelar venda | ✓ | | |
| Desativar cliente | ✓ | | |
| Usuários (`/api/users` e `/api/auth/register`) | ✓ | | |

Os nomes dos perfis ficam em `Auth/Roles.cs`, porque atributos do C# só aceitam constantes:

```csharp
public const string AdminOrEstoquista = Admin + "," + Estoquista;   // vírgula = "um ou outro"
```

Dois `[Authorize]` no mesmo endpoint valem como **"e"**. É assim que o cancelamento fica só para o ADMIN dentro de um controller liberado para ADMIN e VENDEDOR:

```csharp
[Authorize(Roles = Roles.AdminOrVendedor)]      // na classe
public class SalesController ...
    [Authorize(Roles = Roles.Admin)]            // no método: precisa passar nos dois
    [HttpPost("{id:int}/cancel")]
```

O vendedor consulta produtos porque o PDV precisa buscar produtos. O vendedor não vê compras nem lotes porque isso envolve custo de fornecedor e operação de estoque.

---

## 8. Usuários: as regras

`Services/UserService.cs`, usado pelo `UsersController` (só ADMIN):

1. **E-mail único, sem diferenciar maiúsculas**: gravado sempre em minúsculas. "Ana@Adega.com" e "ana@adega.com" são o mesmo login.
2. **Ninguém desativa o próprio usuário**: evita o ADMIN se trancar fora sem querer.
3. **Sempre sobra um ADMIN ativo**: desativar o último ADMIN, ou mudar o perfil dele, é recusado. Sem isso, o sistema poderia ficar sem ninguém para cadastrar usuários.
4. **Senha na edição é opcional**: em branco mantém a atual. É assim que o ADMIN redefine a senha de quem esqueceu.
5. **Desativar não apaga** (soft delete): compras, vendas e movimentações continuam apontando para o usuário.
6. `POST /api/auth/register` faz o mesmo que `POST /api/users`. Não existe cadastro aberto: num sistema interno, só o ADMIN cria contas.

**Trocar a própria senha** (`PUT /api/auth/me/password`) exige a senha atual. Senha atual errada devolve **400**, não 401: o frontend trata 401 como "sessão expirada" e deslogaria a pessoa só porque ela errou a senha atual.

---

## 9. CurrentUserService: a peça que já estava pronta

Desde a Fase 2, todo service que precisa saber "quem fez" (compras, movimentações, vendas, cancelamentos) depende da interface `ICurrentUserService`. Até agora a implementação devolvia sempre o admin (Id 1). Na Fase 5 só a implementação mudou:

```csharp
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public int UserId => /* lê a claim "sub" do token da requisição */;
}
```

**Nenhum service precisou mudar.** É o ganho de depender de uma interface: a forma de descobrir o usuário mudou por completo, e quem usa nem ficou sabendo. A partir de agora, a venda registra o vendedor que realmente vendeu, e o cancelamento registra o ADMIN que cancelou.

---

## 10. Backend: arquivos novos e alterados

| Arquivo | O que faz |
|---|---|
| `Auth/AuthenticationSetup.cs` | Configura JwtBearer, a validação do Secret, as respostas 401/403, a conferência no banco e a FallbackPolicy |
| `Auth/JwtOptions.cs` | A seção `Jwt` da configuração |
| `Auth/Roles.cs`, `Auth/AppClaims.cs` | Nomes dos perfis e das claims |
| `Services/TokenService.cs` | Cria o JWT |
| `Services/AuthService.cs` | Login, usuário logado e troca de senha |
| `Services/UserService.cs` | CRUD de usuários com as regras da seção 8 |
| `Services/AdminPasswordInitializer.cs` | Senha inicial do admin a partir da configuração |
| `Services/CurrentUserService.cs` | Agora lê o usuário do token |
| `Controllers/AuthController.cs`, `UsersController.cs` | Endpoints novos |
| Todos os controllers antigos | `[Authorize(Roles = ...)]` conforme a tabela da seção 7 |
| `Middleware/GlobalExceptionHandler.cs` | `UnauthorizedException` → 401 |
| `Program.cs` | `AddJwtAuthentication`, `PasswordHasher`, `UseAuthentication`, botão Authorize do Swagger, `public partial class Program` (para os testes) |

**Não há migration nesta fase.** A tabela `Users` já tinha `PasswordHash`, `Role`, `Active` e e-mail único desde a Fase 2.

### Swagger com token

O Swagger agora tem o botão **Authorize**. Faça o login em `POST /api/auth/login`, copie o `token` da resposta, clique em Authorize e cole (só o token, sem "Bearer"). As próximas chamadas vão com o cabeçalho certo.

---

## 11. Testes

```bash
dotnet test
```

São 178 testes. Os novos:

| Arquivo | O que testa |
|---|---|
| `Services/AuthServiceTests.cs` | Token com as claims certas, e-mail sem diferenciar maiúsculas, mensagem genérica para senha errada/e-mail inexistente/usuário inativo, admin sem senha não entra, troca de senha |
| `Services/UserServiceTests.cs` | Hash em vez da senha, e-mail duplicado, senha fraca, editar sem trocar a senha, não desativar a si mesmo, sempre um ADMIN ativo, filtros |
| `Auth/AuthorizationTests.cs` | **Testes de ponta a ponta**: sobem a API inteira em memória, fazem login de verdade e chamam os endpoints por HTTP |

O `AuthorizationTests` usa o `WebApplicationFactory<Program>` (pacote `Microsoft.AspNetCore.Mvc.Testing`), configurado em `Infrastructure/ApiFactory.cs`:

- troca o MySQL por SQLite em memória (remove o registro do `DbContext` e registra de novo);
- usa um `Jwt:Secret` de teste e o ambiente `Testing`, então não lê os seus User Secrets;
- define `Admin:InitialPassword`, então o `AdminPasswordInitializer` também é testado.

Ele confere uma matriz de permissões (cada perfil × cada área → 200 ou 403), 401 sem token e com token falso, e que um usuário desativado ou com perfil alterado perde o acesso na hora, mesmo com um token ainda dentro da validade.

Esses testes pegam um tipo de erro que os testes de service não pegam: esquecer um `[Authorize]` num controller.

---

## 12. Frontend

| Peça | Arquivo | O que faz |
|---|---|---|
| Sessão | `auth/authStorage.ts` | Guarda `{ token, expiresAt, user }` no localStorage. Token vencido é descartado ao ler |
| Contexto | `auth/AuthProvider.tsx` + `useAuth.ts` | Deixa `user`, `login`, `logout` e `hasRole` disponíveis em qualquer componente. Ao abrir o sistema, confirma o usuário em `/auth/me` |
| Interceptors | `api/http.ts` | Antes de cada requisição, anexa `Authorization: Bearer <token>`. Num 401, limpa a sessão e manda para `/login?sessao=expirada` |
| Porteiro das rotas | `auth/RequireAuth.tsx` | Sem login → `/login` (lembrando a página pedida). Sem o perfil → "Acesso negado" |
| Permissões | `auth/permissions.ts` | Quais perfis veem cada área, espelho da seção 7 |
| Telas | `pages/LoginPage.tsx`, `pages/users/UsersPage.tsx`, `UserFormModal.tsx`, `components/ChangePasswordModal.tsx` | Login, gestão de usuários e troca de senha |
| Layout | `layouts/AdminLayout.tsx` | Menu filtrado pelo perfil; embaixo, nome, perfil, "Alterar minha senha" e "Sair" |

Depois do login, o vendedor cai direto no PDV; os outros perfis, no Dashboard.

Botões escondidos conforme o perfil: o vendedor não vê "Novo produto", "Editar" e "Custos"; só o ADMIN vê "Cancelar venda" e "Desativar cliente".

**Esconder no frontend é conforto, não segurança.** Qualquer um pode abrir o DevTools e chamar a API direto. Quem garante a regra é o backend, que responde 403. O frontend só evita mostrar um botão que daria erro.

---

## 13. Decisões desta fase

1. **Token no localStorage**, enviado no cabeçalho `Authorization`. É o jeito mais simples de entender e depurar. A alternativa (cookie `HttpOnly`) protege melhor contra scripts maliciosos na página, mas exige cuidar de CSRF e de CORS com credenciais. Para um sistema interno, sem conteúdo de terceiros na página, o localStorage é um bom equilíbrio. Fica registrado como melhoria possível.
2. **Sem refresh token.** O token dura um turno (8 horas). Venceu, a pessoa entra de novo. Refresh token resolve sessões longas, mas dobra a complexidade.
3. **Conferência no banco a cada requisição**, para desativar e mudar perfil valerem na hora (seção 6).
4. **Fechado por padrão** (FallbackPolicy).
5. **Cadastro de usuário só pelo ADMIN.** O `/api/auth/register` existe porque estava no plano, mas exige ADMIN.
6. **Mesma mensagem para qualquer falha de login.**
7. **Sem migration**: a tabela de usuários já estava pronta desde a Fase 2.
8. **Senha do admin pela configuração**, gravada como hash na primeira subida, para nenhuma senha ir para o repositório.

---

## 14. Como testar na sua máquina

**1. Atualize o código** (com a API parada, para não travar o build):

```bash
git checkout main
git pull
```

**2. Configure os dois segredos novos** (uma vez só, na raiz do projeto). Eles ficam nos User Secrets, fora do repositório, junto da connection string:

```bash
# Uma chave aleatória com pelo menos 32 caracteres. No PowerShell, este comando gera uma de 64:
#   [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
dotnet user-secrets set "Jwt:Secret" "COLE_AQUI_A_CHAVE_ALEATORIA" --project backend/src/AdegaSystem.Api

# A senha do primeiro login do admin (pelo menos 8 caracteres, com letras e números).
dotnet user-secrets set "Admin:InitialPassword" "SUA_SENHA_DE_ADMIN" --project backend/src/AdegaSystem.Api

dotnet user-secrets list --project backend/src/AdegaSystem.Api   # confere
```

**3. Rode a API e o frontend.** Não há migration nesta fase.

```bash
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http
# em outro terminal
cd frontend && npm run dev
```

No log da API deve aparecer "Senha inicial do admin (admin@adega.local) definida a partir da configuração." O banco continua sendo o container `adega-mysql` na porta 3307.

**Roteiro sugerido:**

1. Abra http://localhost:5173: você cai na tela de login. Entre como `admin@adega.local` com a senha do passo 2.
2. Em **Usuários**, cadastre um vendedor e um estoquista (teste uma senha fraca para ver a mensagem).
3. Saia (menu no canto inferior esquerdo) e entre como o **vendedor**: ele cai no PDV, o menu não tem Compras, Estoque, Lotes, Fornecedores nem Usuários, e em Produtos não há botões de cadastro. Abra `/compras` pela barra de endereço: "Acesso negado". No histórico de vendas, os detalhes não têm "Cancelar venda".
4. Ainda como vendedor, use **Alterar minha senha**. Erre a senha atual de propósito: aparece o erro e você continua logado.
5. Entre como o **estoquista**: tem Compras, Estoque e cadastro de produtos, mas não tem Vendas nem Clientes.
6. Com o estoquista logado numa janela, entre como admin em outra janela anônima e desative o estoquista. Volte à janela do estoquista e clique em qualquer menu: ele volta para o login com "Sua sessão expirou".
7. No Swagger (http://localhost:5080/swagger), chame um endpoint sem token (401), faça login, clique em **Authorize**, cole o token e chame de novo.

Se a API não subir com a mensagem "Jwt:Secret não configurado...", o passo 2 não foi feito (ou a chave tem menos de 32 caracteres).
