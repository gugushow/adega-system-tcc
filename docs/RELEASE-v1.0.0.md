# Adega System v1.0.0: notas da release

Primeira versão estável do Adega System, um sistema web de gestão para adegas: catálogo, compras com lotes e validade,
estoque, PDV, vendas, clientes com fidelidade, dashboard, relatórios e auditoria.

Esta versão reúne as 8 fases do [plano](PLANO.md) e a [etapa de UI/UX](ETAPA-UI-UX.md). A lista de mudanças está no
[CHANGELOG](../CHANGELOG.md) e o passo a passo para rodar está no [README](../README.md).

## Resumo

- Backend ASP.NET Core (.NET 10) com EF Core 10 e MySQL 9.7; frontend React 19 + TypeScript com Vite e React Bootstrap.
- Estoque sempre por lote, com uma movimentação registrada para cada entrada e saída.
- Venda com baixa FEFO (vence primeiro, sai primeiro) e lucro calculado pelo custo real de cada lote.
- Três perfis de acesso (ADMIN, VENDEDOR, ESTOQUISTA) com login JWT.
- Deploy com Docker Compose (MySQL + API + nginx).

## Módulos

| Módulo | O que entrega |
| --- | --- |
| Categorias e Produtos | Cadastro, busca por nome/marca/código de barras, filtros de estoque baixo e zerado, margem e alerta de margem mínima |
| Fornecedores | Cadastro com CNPJ validado e resumo de compras |
| Compras | Vários itens por compra, um lote por item, entrada no estoque e histórico de custo, numa transação |
| Cadastro rápido na compra | Produto novo cadastrado no próprio fluxo da compra, já selecionado na linha |
| Estoque e movimentações | Extrato com filtros; ajuste de inventário e perda por lote, com motivo |
| Lotes e Validade | Vencidos e vencendo em 7/30/60 dias; baixa de lote vencido |
| FEFO | A venda escolhe os lotes pela validade; lote vencido nunca é vendido |
| Clientes | CPF opcional e validado, histórico, ticket médio |
| PDV | Busca e leitor de código de barras, combos, cliente, pagamento, desconto, atalhos F2/F9 |
| Vendas e Cancelamento | Histórico com lotes usados, custo e lucro; cancelamento (ADMIN) devolve cada unidade ao lote de origem |
| Lucro e custos | Custo do produto = última compra pela data da nota; lucro da venda pelo custo dos lotes vendidos |
| Autenticação e Perfis | JWT de 8 horas, senhas em hash, menu e ações filtrados por perfil |
| Dashboard | Vendas e faturamento do dia, faturamento e lucro do mês, alertas clicáveis, gráficos e rankings |
| Relatórios | Vendas, produtos mais/menos vendidos, categorias, parados, margens, compras por fornecedor, valor do estoque |
| Combos | Preço especial; no PDV o combo vira um item por produto com a economia rateada como desconto |
| Fidelidade | R$ 10 = 1 ponto (configurável), resgate, ajuste pelo ADMIN, estorno no cancelamento |
| Auditoria | Quem fez, quando, e o antes/depois das operações sensíveis |
| Histórico de custos | Tabela e gráfico da evolução do custo de cada produto |
| Tema | Claro, escuro ou do sistema, guardado no navegador |

## Decisões técnicas

- **Um projeto de Web API organizado por pastas** (`Controllers`, `Services`, `Validators`, `DTOs`, `Entities`, `Data`,
  `Auth`...) **e um projeto de testes.** Para o tamanho do sistema, mais projetos ou camadas só trariam complexidade.
- **Services usam o `AdegaDbContext` diretamente**, sem repositório genérico: o EF Core já faz esse papel.
- **`StockService` é o único caminho para alterar o estoque.** `Product.CurrentStock` tem setter privado; o service
  atualiza o saldo do lote, o estoque do produto e grava a `StockMovement` na mesma transação. O estoque do produto é
  sempre a soma dos saldos dos lotes.
- **FEFO com rastreio por lote:** `SaleItemBatch` guarda quanto saiu de cada lote em cada item. Isso permite calcular o
  lucro pelo custo real e, no cancelamento, devolver cada unidade ao lote certo.
- **Desconto da venda rateado entre os itens**, para o lucro por produto e por categoria fechar com o total.
- **Relatórios no horário de Brasília:** as datas são gravadas em UTC e agrupadas pelo dia de Brasília (por isso a
  imagem da API usa a base Ubuntu, que tem o fuso horário, e não a Alpine).
- **Provider MySQL oficial da Oracle** (`MySql.EntityFrameworkCore`), porque o Pomelo não tinha versão estável para o
  EF Core 10. Ele não lê `DateOnly` corretamente, então o projeto usa um conversor próprio (`Data/DateOnlyConverter.cs`).
- **JWT conferido no banco a cada requisição:** usuário desativado ou com perfil alterado perde o acesso na hora.
- **Concorrência:** resgate de pontos em transação serializável; conflitos e cadastros duplicados simultâneos viram `409`.
- **Soft delete** em todos os cadastros: `DELETE` apenas desativa.
- **Frontend:** uma pasta por tela, uma função por endpoint em `src/api`, menu definido como dados (`layouts/menu.ts`) e
  todas as cores do tema num único arquivo (`styles/tokens.css`).

## Segurança

Revisão feita na preparação desta release:

- **Secrets fora do Git:** connection string, `Jwt:Secret` e `Admin:InitialPassword` vêm de User Secrets em
  desenvolvimento e de variáveis de ambiente em produção. Busca nos arquivos versionados e no histórico: nenhuma senha
  real, chave JWT ou connection string de produção. Os únicos valores presentes são os de exemplo do banco de
  desenvolvimento local (`.env.example`).
- **`.env` e `.env.prod` ignorados:** o `.gitignore` ignora `.env` e `.env.*`, exceto os arquivos `.example` (conferido
  com `git check-ignore`).
- **Ajuste desta release:** o `.env.prod.example` trazia valores de exemplo (como `TROQUE_...`) para as senhas e a chave
  JWT. A chave de exemplo tinha mais de 32 caracteres e passaria na validação da API, ou seja, quem esquecesse de trocá-la
  subiria a produção com uma chave pública. Agora esses campos vêm vazios e o `docker compose` se recusa a subir até que
  sejam preenchidos (conferido com `docker compose config`). Nenhuma regra de negócio mudou.
- **JWT:** a API não sobe sem `Jwt:Secret` ou com menos de 32 caracteres; tokens valem 8 horas; issuer e audience são
  validados.
- **Login:** limite de 10 tentativas por minuto por endereço (`429`); senhas guardadas só como hash (PasswordHasher do
  ASP.NET Core).
- **Produção:** Swagger/OpenAPI desligados fora do ambiente de desenvolvimento; HSTS ligado; CORS liberado só para o
  frontend de desenvolvimento (em produção o frontend usa o mesmo endereço); API rodando sem root no contêiner; MySQL sem
  porta exposta fora do Docker; migrations aplicadas na subida.
- **nginx:** `server_tokens off`, limite de 1 MB por requisição, cabeçalhos `X-Content-Type-Options`, `X-Frame-Options`
  e `Referrer-Policy`, e `index.html` sem cache (os assets com hash ficam em cache por 1 ano).

## Testes

Executados na preparação desta release:

| Verificação | Resultado |
| --- | --- |
| `dotnet build` | Sucesso, 0 avisos e 0 erros |
| `dotnet test` | 341 testes: 341 aprovados, 0 falhas, 0 ignorados |
| `npm run lint` | Sem erros |
| `npm run build` | Sucesso (checagem de tipos + build de produção) |

Validação manual no navegador (Chromium), com a API e o MySQL rodando localmente:

- Health check (`/health`) respondendo `Healthy`.
- Login como ADMIN e carregamento do dashboard, sem erros no console.
- Venda simples no PDV (2 itens, Pix) finalizada com sucesso.
- Tema claro e escuro: escolhidos pelo menu, mantidos depois de recarregar a página, e o modo "Sistema" seguindo o navegador.
- Telas de Produtos, Compras, Lotes, Estoque, PDV e Relatórios nos dois temas (são as screenshots do README).

O backend tem testes de regras de negócio (services contra SQLite em memória), de validação, de permissões e de
comportamento HTTP (API inteira em memória com `WebApplicationFactory`). O frontend não tem testes automatizados.

## Limitações conhecidas

- Trocar a senha não derruba sessões abertas em outros computadores até o token vencer (no máximo 8 horas).
- Listas não ordenam pelo cabeçalho da coluna; relatórios não exportam para PDF/Excel.
- Os filtros de fornecedor e de usuário carregam até 100 itens.
- Sem emissão de cupom ou nota fiscal: o sistema é gerencial, não fiscal.
- O frontend não tem testes automatizados e o repositório não tem CI: as verificações são rodadas localmente.
- O provider MySQL da Oracle precisa do conversor próprio para `DateOnly`; colunas de data novas devem ser testadas
  contra o MySQL real, porque os testes usam SQLite.

## Melhorias futuras

Backlog sugerido para depois da v1.0.0 (nada disso está implementado):

1. Impressão de comprovante da venda (cupom não fiscal).
2. Leitor de código de barras com cadastro do código direto pelo PDV e pela compra.
3. Exportação de relatórios para PDF e Excel.
4. Deploy real com HTTPS e backup automático do banco.
5. Notificações de estoque baixo e de lotes perto do vencimento.
6. PDV: pagamento dividido em mais de uma forma e cálculo de troco.
7. Novos relatórios: curva ABC, vendas por vendedor e por horário.
8. Integração fiscal (NFC-e) com um provedor homologado.

## Como publicar a versão

A tag e a release no GitHub só são criadas depois do merge deste PR e da confirmação do responsável pelo projeto:

```bash
git checkout main
git pull
git tag -a v1.0.0 -m "Adega System v1.0.0"
git push origin v1.0.0
```

Depois, criar a release `v1.0.0` no GitHub a partir da tag, usando este arquivo como descrição.
