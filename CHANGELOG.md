# Changelog

Todas as mudanças relevantes do Adega System ficam registradas aqui.

O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto usa
[Versionamento Semântico](https://semver.org/lang/pt-BR/): `MAJOR.MINOR.PATCH`.

## [Não lançado]

### Adicionado

- **Leitor de código de barras pela câmera** no cadastro/edição de produto e no PDV (EAN-13, EAN-8, UPC-A, UPC-E), com a API nativa `BarcodeDetector` ou a biblioteca `barcode-detector` (ZXing em WebAssembly, empacotado no build). Ver [docs/LEITOR-CODIGO-DE-BARRAS.md](docs/LEITOR-CODIGO-DE-BARRAS.md).
- PDV: "Produto não encontrado." e atalho "Cadastrar produto" (só para quem pode cadastrar) com o código preenchido.
- **Produtos:** botão "Escanear código" na pesquisa; o produto lido aparece na própria lista.
- **Consulta rápida de produto** na tela inicial ("Consultar produto"): preço, estoque e situação para todos os perfis, sem custo nem margem; próximos lotes e alerta de lote vencido só para ADMIN e ESTOQUISTA.
- Testes do frontend com Vitest, Testing Library e jsdom (`npm test`).
- **Dados de demonstração** para o ambiente local: comandos `seed-demo` e `use-demo` num banco separado (`adega_demo`, connection string `DemoConnection`), só em Development. Carga pelos services reais (compras, FEFO, lotes, custo, fidelidade) com 44 produtos, 8 fornecedores, 25 clientes, 20 compras, 36 vendas e 8 combos. Ver [docs/DADOS-DEMO.md](docs/DADOS-DEMO.md).
- **Demonstração só com Docker** (`docker-compose.demo.yml`, projeto `adega-demo`): MySQL próprio com o `adega_demo`, carga pelo `seed-demo` existente em Development, API com `use-demo` e frontend em `127.0.0.1:8081`. Separado da produção e sem internet depois do primeiro build. Ver [docs/DEMO-DOCKER.md](docs/DEMO-DOCKER.md).

### Alterado

- A busca por código de barras encontra um produto salvo como UPC-A (12 dígitos) quando lido como EAN-13 com 0 na frente, e vice-versa. O exato tem prioridade e os códigos salvos não mudam.
- Cadastro e edição de produto recusam um código cuja forma equivalente UPC-A/EAN-13 já pertence a outro produto.

## [1.0.0] - 2026-10-03

Primeira versão estável. Reúne as 8 fases do plano ([docs/PLANO.md](docs/PLANO.md)) e a etapa de UI/UX.
Detalhes em [docs/RELEASE-v1.0.0.md](docs/RELEASE-v1.0.0.md).

### Adicionado

- **Catálogo:** categorias e produtos com busca, filtros, código de barras, margem calculada e alerta de margem abaixo do mínimo (Fase 1).
- **Fornecedores e compras:** fornecedores com validação de CNPJ e resumo de compras; compras com vários itens, entrada no estoque e histórico de custo (Fase 2).
- **Estoque centralizado:** `StockService` como único caminho para alterar o estoque, sempre com movimentação e transação; extrato e ajustes manuais com motivo (Fase 2).
- **Lotes e validade:** um lote por item de compra, estoque do produto igual à soma dos lotes, consultas de vencidos e vencendo em 7/30/60 dias, baixa de lotes vencidos (Fase 3).
- **Clientes e vendas:** clientes com CPF opcional, PDV, baixa FEFO pelos lotes, lucro pelo custo real de cada lote e cancelamento com devolução ao lote de origem (Fase 4).
- **Autenticação e perfis:** login com JWT (8 horas), perfis ADMIN, VENDEDOR e ESTOQUISTA, gestão de usuários e troca da própria senha (Fase 5).
- **Dashboard e relatórios:** cards, alertas e gráficos do dia e do mês; relatórios de vendas, produtos, categorias, produtos parados, margens, compras e estoque (Fase 6).
- **Combos, fidelidade e auditoria:** combos com preço especial, programa de pontos com resgate e estorno, auditoria com antes e depois e gráfico do histórico de custos (Fase 7).
- **Qualidade e deploy:** limite de tentativas de login, respostas 409 para conflitos, testes de HTTP e de erros, deploy com Docker Compose e nginx (Fase 8).
- **Interface:** tema claro, escuro ou do sistema, menu lateral recolhível com ícones, PDV redesenhado e cadastro rápido de produto dentro da compra (etapa de UI/UX).
- Documentação da release: README reorganizado, [docs/API.md](docs/API.md), screenshots reais em `docs/telas/v1/` e estas notas.
- Versão `1.0.0` no `frontend/package.json` e no projeto da API.

### Segurança

- Connection string, chave JWT e senha inicial do admin vêm só de User Secrets (desenvolvimento) ou variáveis de ambiente (produção); nenhum valor real é versionado.
- `.env` e `.env.prod` ignorados pelo Git (apenas os `.example` são versionados).
- O `.env.prod.example` agora traz as senhas e a chave JWT **vazias**: o `docker compose` de produção se recusa a subir até que sejam preenchidas, evitando colocar o sistema no ar com um valor de exemplo público.
- Swagger/OpenAPI só no ambiente de desenvolvimento; HSTS fora dele; API sem privilégios de root no contêiner; MySQL sem porta exposta em produção.
