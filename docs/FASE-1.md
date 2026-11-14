# Fase 1: Categorias e Produtos (guia de estudo)

Este guia acompanha o PR da Fase 1. O código completo está no PR. Aqui você encontra, para cada arquivo, onde ele fica, para que serve e o que vale a pena entender nele. No fim, o caminho completo de uma requisição e as decisões tomadas.

---

## 1. Como uma requisição percorre o backend

### POST /api/products (cadastrar produto)

```
Navegador (ProductFormModal)
  ↓ productsApi.create(input)  →  Axios POST /api/products  (o Vite encaminha para :5080)
ASP.NET Core
  ↓ UseExceptionHandler        (envolve tudo o que vem depois)
  ↓ Roteamento                  →  ProductsController.Create(CreateProductDto dto)
  ↓ [ApiController] lê o JSON   →  se o JSON nem puder ser lido: 400 "Verifique o formato dos campos"
ProductsController.Create
  ↓ productService.CreateAsync(dto)
ProductService.CreateAsync
  ↓ 1. createValidator.ValidateAndThrowAsync(dto)   →  regras de formato (FluentValidation)
  ↓ 2. NormalizeBarcode                              →  "" vira null
  ↓ 3. EnsureCategoryIsActiveAsync                   →  consulta o banco
  ↓ 4. EnsureBarcodeIsAvailableAsync                 →  consulta o banco
  ↓ 5. new Product + ApplyInput                      →  CurrentStock fica 0
  ↓ 6. SaveChangesAsync                              →  INSERT no MySQL
  ↓ 7. GetByIdAsync                                  →  recarrega com a categoria e converte para ProductDto
ProductsController
  ↓ 201 Created + Location: /api/products/{id} + ApiResponse<ProductDto>
Navegador
  ↓ fecha o modal, mostra "Produto cadastrado com sucesso." e recarrega a lista
```

**Se algo falhar no caminho**, o service lança uma exceção e para ali mesmo:

| Exceção | Quem lança | Resposta |
|---|---|---|
| `ValidationException` | FluentValidation (passo 1) | 400 + lista de campos com erro |
| `BusinessException` | regras dos passos 3 e 4 | 400 + mensagem |
| `NotFoundException` | busca por id | 404 + mensagem |
| qualquer outra | bug ou banco fora do ar | 500 + mensagem genérica (o detalhe vai para o log) |

Quem transforma a exceção em resposta é o `GlobalExceptionHandler`. Por isso nenhum controller tem `try/catch`.

### GET /api/products?search=heineken&stock=Low

```
ProductsController.GetAll([FromQuery] ProductQuery query)
  ↓ ProductService.GetAllAsync
      monta o IQueryable aos poucos (Where de busca, categoria, ativo, estoque)
      CountAsync   → SELECT COUNT(*) ...           (total para a paginação)
      Skip/Take    → SELECT ... LIMIT 20 OFFSET 0  (só a página pedida)
      ToDto()      → calcula margem, IsLowStock, IsOutOfStock em memória
  ↓ 200 + ApiResponse<PagedResult<ProductDto>>
```

---

## 2. Backend: arquivo por arquivo

### Common/ApiResponse.cs
**Caminho:** `backend/src/AdegaSystem.Api/Common/ApiResponse.cs`
**Para que serve:** o formato único de resposta: `{ success, message, data, errors }`.
**Pontos importantes:**
- `ApiResponse<T>.Ok(data, message)` para sucesso e `ApiResponse.Fail(message, errors)` para erro.
- `ApiError(Field, Message)` permite ao frontend marcar o campo certo do formulário. Esta é uma pequena extensão do formato que você sugeriu (`errors: []`): cada erro diz qual campo está errado.

### Common/PagedResult.cs e Common/PagedQuery.cs
**Para que servem:** paginação reutilizável. `PagedQuery` recebe `?page=&pageSize=` e corrige valores absurdos (`pageSize` fica entre 1 e 100; `page` é no mínimo 1). `PagedResult` devolve os itens e as contagens (`totalItems`, `totalPages`).
**Por quê:** toda listagem futura (fornecedores, vendas...) herda de `PagedQuery` e ganha paginação de graça.

### Common/Exceptions/BusinessException.cs e NotFoundException.cs
**Para que servem:** dar nome aos tipos de erro. O service escreve `throw new BusinessException("Já existe um produto com esse código de barras.")` e não precisa saber nada de HTTP.

### Middleware/GlobalExceptionHandler.cs
**Para que serve:** tratamento global de exceções (o item 27 do enunciado).
**Pontos importantes:**
- Implementa `IExceptionHandler`, a forma moderna do .NET 8+ (em vez de escrever um middleware manual com `try/catch`).
- Um `switch` decide status e mensagem por tipo de exceção.
- Converte o nome do campo para camelCase (`SalePrice` → `salePrice`), igual ao JSON.
- Erros 500 são logados com stack trace, mas o cliente recebe só "Ocorreu um erro inesperado". Isso evita vazar detalhes internos (item 28, segurança).

### Entities/Category.cs
**Para que serve:** a tabela `Categories`.
**Pontos importantes:** `Active` faz o soft delete. `Products` é a navegação 1:N.

### Entities/Product.cs
**Para que serve:** a tabela `Products`.
**Pontos importantes:**
- **`public int CurrentStock { get; private set; }`**: esta é a regra que você reforçou. Nenhum código fora da classe consegue escrever `product.CurrentStock = 10`; o compilador recusa. O EF Core ainda consegue preencher o valor ao ler do banco. Na Fase 2, a única porta de entrada será a lógica centralizada de estoque.
- `IsLowStock` e `IsOutOfStock` são propriedades calculadas (`CurrentStock <= MinimumStock`, `CurrentStock == 0`). Elas não viram colunas.

### Data/AdegaDbContext.cs
**Para que serve:** a "porta" para o banco. Já existia no esqueleto; ganhou os `DbSet`s e duas configurações:
- `ApplyConfigurationsFromAssembly` registra todas as classes de `Data/Configurations` sozinho.
- `ConfigureConventions` aplica o `UtcDateTimeConverter` a todas as datas.

### Data/UtcDateTimeConverter.cs
**Para que serve:** corrigir um detalhe que pega muita gente. O MySQL guarda `datetime` sem fuso. Gravamos em UTC, mas ao ler o .NET recebia a data como "sem fuso", e o JSON saía sem o `Z` no final. O navegador então mostraria a hora errada (3 horas de diferença no Brasil). O conversor marca toda data lida como UTC.

### Data/Configurations/CategoryConfiguration.cs e ProductConfiguration.cs
**Para que servem:** Fluent API: tamanhos de coluna, precisão de decimais, índices e relacionamentos.
**Pontos importantes:**
- `HasPrecision(12, 4)` no custo e `(12, 2)` no preço de venda. O custo unitário de uma caixa (R$ 46,77 / 12 = R$ 3,8975) precisa de 4 casas.
- `HasIndex(p => p.Barcode).IsUnique()`: o MySQL aceita vários `NULL` num índice único, então produtos sem código de barras não conflitam. Por isso o service converte `""` em `null`.
- Índices em `Barcode`, `Name` e `CategoryId` (este último o EF cria sozinho para a chave estrangeira).
- `OnDelete(DeleteBehavior.Restrict)`: o padrão do EF seria Cascade, que apagaria todos os produtos ao apagar uma categoria. Com Restrict o banco recusa.

### Data/Seed/CategorySeed.cs
**Para que serve:** as 10 categorias iniciais (Cerveja, Vinho, Whisky...). Entram pela migration via `HasData`.
**Ponto importante:** a data é fixa. Com `DateTime.UtcNow`, cada nova migration acharia que o seed mudou e geraria um `UPDATE` inútil.

### Data/Migrations/*_InitialCreate.cs
**Para que serve:** o script versionado que cria as tabelas, índices e o seed. Foi gerado com `dotnet ef migrations add InitialCreate` e aplicado com `dotnet ef database update` no MySQL 9.7.

### DTOs/Categories/CategoryDtos.cs e DTOs/Products/ProductDtos.cs
**Para que servem:** os contratos da API. Os controllers nunca recebem nem devolvem entidades (item 26).
**Pontos importantes:**
- `CreateProductDto` e `UpdateProductDto` **não têm `CurrentStock`**. Mesmo que alguém mande o campo no JSON, ele é ignorado.
- `ProductDto` já traz `categoryName`, `marginPercent`, `isLowStock` e `isOutOfStock`, para a tela não precisar calcular.
- `IProductInput` é a interface comum às duas DTOs de entrada; serve para escrever as regras de validação uma vez só.
- `records` são imutáveis e comparam por valor: ideais para DTOs.

### Validators/CategoryValidators.cs e ProductValidators.cs
**Para que servem:** regras de formato com FluentValidation (nome obrigatório, preço de venda > 0, teor entre 0 e 100...).
**Pontos importantes:**
- `ProductInputValidator` valida `IProductInput`, e os validators de Create e Update fazem `Include` dele.
- Regras que precisam do banco (código de barras repetido, categoria inativa) ficam no service. O validator só olha a DTO.
- Não bloqueamos preço de venda menor que o custo: promoções existem. O alerta de margem mínima vem na Fase 6.

### Mappings/CategoryMappings.cs e ProductMappings.cs
**Para que servem:** converter entidade → DTO com métodos de extensão (`product.ToDto()`).
**Ponto importante:** `CalculateMarginPercent` implementa `((SalePrice - CostPrice) / SalePrice) * 100` com 2 casas. Exemplo do enunciado: custo 8,00 e venda 8,50 dão 5,88%, e isso está coberto por um teste.

### Services/ICategoryService.cs, CategoryService.cs
**Para que serve:** regras de categoria.
**Pontos importantes:**
- Nome único **sem diferenciar maiúsculas** ("cerveja" = "Cerveja"), com `ToLower()` dos dois lados.
- Não deixa desativar uma categoria com produtos ativos. Se deixasse, a categoria sumiria dos selects e os produtos ficariam numa categoria "invisível".
- `ProductCount` conta só os produtos ativos, com uma subconsulta traduzida para SQL.

### Services/IProductService.cs, ProductService.cs
**Para que serve:** regras de produto.
**Pontos importantes:**
- `GetAllAsync` monta a consulta aos poucos; nada vai ao banco até o `CountAsync`/`ToListAsync`.
- A busca usa `ToLower()` para funcionar igual no MySQL e no SQLite dos testes.
- O filtro de estoque escreve a condição por extenso (`p.CurrentStock <= p.MinimumStock`), porque o EF não sabe traduzir a propriedade C# `IsLowStock` para SQL.
- `ApplyInput` copia os campos editáveis e deixa `CurrentStock` de fora.
- `DeactivateAsync` só marca `Active = false` (soft delete).

### Controllers/CategoriesController.cs e ProductsController.cs
**Para que servem:** traduzir HTTP para chamadas de service. São "magros": uma linha por endpoint.
**Pontos importantes:**
- `CreatedAtAction` devolve 201 e o cabeçalho `Location` apontando para o novo recurso (padrão REST).
- `/low-stock` e `/out-of-stock` reaproveitam o `GetAll` com o filtro já preenchido.
- `{id:int}` na rota faz `/api/products/abc` dar 404 em vez de chegar ao controller.

### Program.cs
**O que mudou:**
- `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes`: sem isso o ASP.NET valida sozinho as strings obrigatórias e responde em inglês antes do FluentValidation.
- `InvalidModelStateResponseFactory`: quando o JSON nem pode ser lido (texto num campo numérico), responde no nosso formato, sem expor nomes de classes.
- `JsonStringEnumConverter`: enums trafegam como texto (`"Low"`).
- `AddExceptionHandler` + `UseExceptionHandler`: liga o tratamento global.
- `AddValidatorsFromAssemblyContaining<Program>()`: registra todos os validators.
- `AddScoped<IProductService, ProductService>()`: Scoped significa uma instância por requisição, a mesma vida útil do `DbContext`.

### global.json
Ganhou `"test": { "runner": "Microsoft.Testing.Platform" }`. O SDK do .NET 10 exige essa opção para rodar `dotnet test` com o xUnit v3.

---

## 3. Testes

**Caminho:** `backend/tests/AdegaSystem.Tests/`

- `Infrastructure/TestDatabase.cs`: cria um SQLite em memória por classe de teste. Usamos SQLite, e não o provider InMemory do EF, porque ele respeita chaves estrangeiras, índices únicos e transações, como o MySQL.
- `Services/CategoryServiceTests.cs` (6 testes): nome duplicado sem diferenciar maiúsculas, validação, 404, desativação bloqueada com produtos ativos, soft delete.
- `Services/ProductServiceTests.cs` (14 testes): estoque começa em 0, margem 5,88%, categoria inexistente ou inativa, código de barras duplicado, código em branco, validação, **edição nunca altera o estoque**, filtros de estoque baixo e sem estoque, busca com paginação, busca por código de barras, soft delete.

**Como o teste simula estoque se o setter é privado?** Pelo `context.Entry(product).Property(p => p.CurrentStock).CurrentValue = 50`, ou seja, falando direto com o EF. Isso mostra que o código normal da aplicação não tem como mudar o estoque.

Seguimos TDD (desenvolvimento guiado por testes): os testes foram escritos antes e falharam (nem compilavam) até o código existir. Rodar: `dotnet test` (20 testes, todos passando).

---

## 4. Frontend: arquivo por arquivo

| Arquivo | Para que serve | Ponto importante |
|---|---|---|
| `src/api/http.ts` | Instância única do Axios | `unwrap` devolve só o `data` da resposta padrão; `getErrorMessage` e `getFieldErrors` traduzem erros da API para a tela. O token JWT entra aqui na Fase 5. |
| `src/api/categories.ts`, `products.ts` | Uma função por endpoint | As telas nunca montam URLs; só chamam `productsApi.list(...)`. |
| `src/types/*.ts` | Tipos TypeScript espelhando os DTOs | `ProductInput` também não tem `currentStock`. |
| `src/hooks/useDebounce.ts` | Espera o usuário parar de digitar | Sem ele, cada tecla na busca seria uma requisição. |
| `src/utils/format.ts` | Formatação de moeda e % em pt-BR | Usa `Intl.NumberFormat`, nativo do navegador. |
| `src/components/Pagination.tsx` | Paginação reutilizável | Mostra até 5 páginas ao redor da atual. |
| `src/components/SearchInput.tsx` | Campo de busca | |
| `src/components/ConfirmDialog.tsx` | "Tem certeza?" antes de desativar | |
| `src/components/Loading.tsx`, `EmptyState.tsx`, `AppAlert.tsx` | Carregando, lista vazia, mensagem de sucesso/erro | |
| `src/layouts/AdminLayout.tsx` + `.css` | Menu lateral com todos os itens do enunciado | Itens das próximas fases aparecem desabilitados "(em breve)". No celular o menu vira uma gaveta (Offcanvas). |
| `src/pages/categories/CategoriesPage.tsx` | Listagem com busca, filtro de situação e paginação | O `useEffect` busca a lista quando um filtro muda; a variável `ignore` descarta respostas antigas que chegam fora de ordem. |
| `src/pages/categories/CategoryFormModal.tsx` | Cadastro e edição | O modal só é montado quando abre, então o `useState` já nasce com os dados certos. |
| `src/pages/products/ProductsPage.tsx` | Listagem com busca, categoria, estoque e situação | Badge vermelho "Sem estoque" e amarelo "baixo". |
| `src/pages/products/ProductFormModal.tsx` | Cadastro e edição | Campos ficam como texto e viram número no envio (aceita "3,90"). Mostra uma prévia da margem enquanto você digita. Erros de cada campo vêm do backend. |
| `src/pages/products/StockBadge.tsx` | Badge de estoque | |
| `src/pages/HomePage.tsx` | Página inicial provisória | É o card de status do esqueleto; o dashboard real vem na Fase 6. |
| `src/App.tsx` | Rotas (`/`, `/produtos`, `/categorias`) | React Router 8; todas as páginas ficam dentro do `AdminLayout`. |

**Por que não criei um componente `Button` próprio?** O `Button` do React Bootstrap já é reutilizável e tem variantes. Um wrapper só repassaria props (o tipo de overengineering que combinamos evitar). O mesmo vale para `Modal` e `Table`.

**Validação no frontend:** a fonte da verdade é o backend. O formulário mostra os erros que a API devolve, campo a campo. Assim as regras não ficam duplicadas e não divergem.

---

## 5. Decisões desta fase

1. **`CurrentStock` com setter privado** em vez de apenas "combinar" que ninguém altera: o compilador garante a regra.
2. **Desativar categoria com produtos ativos é bloqueado**, com mensagem clara.
3. **Código de barras opcional e único**; vazio vira `null`.
4. **Erros com campo** (`{ field, message }`) para destacar o input certo.
5. **Datas sempre em UTC**, com o conversor para o MySQL.
6. **Busca sem diferenciar maiúsculas** com `ToLower()`. Num catálogo de adega isso é rápido o bastante; se um dia houver dezenas de milhares de produtos, trocamos por um índice FULLTEXT.

## 6. Como testar na sua máquina

```bash
cp .env.example .env
docker compose up -d
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3307;Database=adega;User=adega;Password=SUA_SENHA;" --project backend/src/AdegaSystem.Api
dotnet tool restore
dotnet ef database update --project backend/src/AdegaSystem.Api
dotnet run --project backend/src/AdegaSystem.Api --launch-profile http
# em outro terminal
cd frontend && npm install && npm run dev
```

Abra http://localhost:5173/produtos e http://localhost:5080/swagger.
