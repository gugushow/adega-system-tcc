# API do Adega System

Referência dos endpoints da API REST (versão 1.0.0). Com a API rodando em desenvolvimento, a mesma lista aparece,
interativa, no Swagger UI: http://localhost:5080/swagger (só no ambiente de desenvolvimento).

No Swagger, faça login em `POST /api/auth/login`, clique em **Authorize** e cole o token recebido.

Todas as respostas seguem o formato `{ "success", "message", "data", "errors" }`. `DELETE` apenas desativa o registro.
Todas as rotas exigem o cabeçalho `Authorization: Bearer <token>`, menos o login e o `/health`.

| Status | Quando |
| --- | --- |
| 400 | Dados inválidos (`errors` traz o campo, como no JSON: `name`, `items[0].quantity`) ou regra de negócio |
| 401 | Sem token, token vencido ou usuário desativado; login com e-mail ou senha errados |
| 403 | O perfil não tem permissão |
| 404 | Registro não encontrado |
| 409 | Outra operação mudou os mesmos dados ao mesmo tempo (tente de novo) ou cadastro duplicado simultâneo |
| 429 | Mais de 10 tentativas de login por minuto do mesmo endereço |

| Método | Rota | Descrição |
| --- | --- | --- |
| POST | `/api/auth/login` | Login: devolve `token`, `expiresAt` e o usuário (público) |
| GET | `/api/auth/me` | Usuário logado |
| PUT | `/api/auth/me/password` | Troca a própria senha (pede a senha atual) |
| POST | `/api/auth/register` | Cadastra usuário (só ADMIN, igual a `POST /api/users`) |
| GET | `/api/users?search=&role=&active=&page=&pageSize=` | Lista paginada (só ADMIN) |
| GET | `/api/users/{id}` | Detalhe (só ADMIN) |
| POST | `/api/users` | Cadastra (só ADMIN) |
| PUT | `/api/users/{id}` | Edita perfil e status; `newPassword` opcional redefine a senha (só ADMIN) |
| DELETE | `/api/users/{id}` | Desativa (só ADMIN; não vale para si mesmo nem para o último ADMIN) |
| GET | `/api/categories?search=&active=&page=&pageSize=` | Lista paginada |
| GET | `/api/categories/{id}` | Detalhe |
| POST | `/api/categories` | Cadastra |
| PUT | `/api/categories/{id}` | Edita (inclui reativar) |
| DELETE | `/api/categories/{id}` | Desativa (bloqueado se houver produtos ativos) |
| GET | `/api/products?search=&categoryId=&active=&stock=Low\|Out&page=&pageSize=` | Lista paginada |
| GET | `/api/products/low-stock` | Estoque atual ≤ estoque mínimo |
| GET | `/api/products/out-of-stock` | Estoque zerado |
| GET | `/api/products/{id}` | Detalhe |
| GET | `/api/products/barcode/{barcode}` | Busca pelo código de barras (todos os perfis). Tenta o valor exato; se não achar, a forma equivalente UPC-A ⇄ EAN-13 com 0 na frente (só com dígito verificador válido). 404 se não existir. Devolve também produtos inativos |
| POST | `/api/products` | Cadastra (estoque começa em 0) |
| PUT | `/api/products/{id}` | Edita (nunca altera o estoque) |
| DELETE | `/api/products/{id}` | Desativa |
| GET | `/api/products/{id}/cost-history` | Histórico de custo de compra, com variação % |
| GET | `/api/suppliers?search=&active=&page=&pageSize=` | Lista paginada |
| GET | `/api/suppliers/{id}` | Detalhe |
| GET | `/api/suppliers/{id}/purchases` | Compras do fornecedor |
| GET | `/api/suppliers/{id}/summary` | Total comprado, última compra, produtos mais comprados |
| POST | `/api/suppliers` | Cadastra (CNPJ com ou sem máscara) |
| PUT | `/api/suppliers/{id}` | Edita (inclui reativar) |
| DELETE | `/api/suppliers/{id}` | Desativa |
| GET | `/api/purchases?supplierId=&from=&to=&page=&pageSize=` | Lista paginada |
| GET | `/api/purchases/{id}` | Detalhe com itens |
| POST | `/api/purchases` | Registra a compra, cria um lote por item e dá entrada no estoque |
| GET | `/api/stock-movements?productId=&type=&from=&to=&page=&pageSize=` | Extrato de movimentações |
| POST | `/api/stock/adjustments` | Ajuste (`ADJUSTMENT`) ou perda (`LOSS`) num lote (`batchId`) |
| GET | `/api/batches?productId=&onlyAvailable=&status=Expired\|Expiring&days=&page=&pageSize=` | Lotes, ordenados pela validade |
| GET | `/api/batches/expired` | Lotes vencidos com saldo |
| GET | `/api/batches/expiring?days=7\|30\|60` | Lotes que vencem de hoje até N dias |
| GET | `/api/batches/{id}` | Detalhe do lote |
| POST | `/api/batches/{id}/write-off` | Baixa de lote vencido (`EXPIRED`) |
| GET | `/api/customers?search=&active=&page=&pageSize=` | Lista paginada (busca por nome, CPF ou telefone) |
| GET | `/api/customers/{id}` | Detalhe |
| GET | `/api/customers/{id}/summary` | Nº de compras, total gasto, última compra e ticket médio |
| GET | `/api/customers/{id}/sales` | Vendas do cliente |
| POST | `/api/customers` | Cadastra (só o nome é obrigatório) |
| PUT | `/api/customers/{id}` | Edita; ativar/desativar só vale quando quem edita é ADMIN |
| DELETE | `/api/customers/{id}` | Desativa |
| GET | `/api/sales?from=&to=&status=&customerId=&paymentMethod=&page=&pageSize=` | Lista paginada |
| GET | `/api/sales/{id}` | Detalhe com itens, lotes usados, custo e lucro |
| POST | `/api/sales` | Finaliza a venda (itens avulsos e `combos`): FEFO, baixa de estoque, lucro e pontos, numa transação |
| POST | `/api/sales/{id}/cancel` | Cancela com motivo, devolve as unidades aos lotes (`RETURN`) e estorna os pontos (só ADMIN) |
| GET | `/api/dashboard` | Cards e gráficos do dashboard (só ADMIN) |
| GET | `/api/reports/sales?from=&to=&groupBy=Day\|Month` | Resumo de vendas, série por dia ou mês e formas de pagamento (só ADMIN) |
| GET | `/api/reports/products?from=&to=&order=MostSold\|LeastSold&limit=` | Produtos mais ou menos vendidos (só ADMIN) |
| GET | `/api/reports/categories?from=&to=` | Vendas por categoria (só ADMIN) |
| GET | `/api/reports/idle-products?days=` | Produtos com estoque sem venda há N dias (só ADMIN) |
| GET | `/api/reports/margins?onlyBelowMinimum=` | Margem por produto e margem mínima (só ADMIN) |
| GET | `/api/reports/purchases-by-supplier?from=&to=` | Compras por fornecedor (só ADMIN) |
| GET | `/api/reports/stock` | Valor do estoque por categoria (só ADMIN) |
| GET | `/api/settings/margin` | Margem mínima configurada |
| PUT | `/api/settings/margin` | Altera a margem mínima (só ADMIN) |
| GET | `/api/combos?search=&active=&page=&pageSize=` | Combos com preço normal, economia e quantos dá para montar (ADMIN e VENDEDOR) |
| GET | `/api/combos/{id}` | Detalhe (ADMIN e VENDEDOR) |
| POST | `/api/combos` | Cadastra (só ADMIN) |
| PUT | `/api/combos/{id}` | Edita; os itens enviados substituem os atuais (só ADMIN) |
| DELETE | `/api/combos/{id}` | Desativa (só ADMIN) |
| GET | `/api/customers/{id}/loyalty?page=&pageSize=` | Saldo de pontos, regra atual e extrato |
| POST | `/api/customers/{id}/loyalty/redeem` | Resgata pontos (até o saldo) |
| POST | `/api/customers/{id}/loyalty/adjust` | Ajuste manual com motivo (só ADMIN) |
| GET | `/api/audit-logs?entity=&entityId=&action=&userId=&from=&to=&page=&pageSize=` | Auditoria (só ADMIN) |
| GET | `/api/settings/loyalty` | Regra da fidelidade (`enabled`, `amountPerPoint`) |
| PUT | `/api/settings/loyalty` | Altera a regra da fidelidade (só ADMIN) |

Nos relatórios, `from` e `to` são dias no horário de Brasília (`2026-10-01`). Sem eles, valem os últimos 30 dias; o período máximo é de 2 anos.
