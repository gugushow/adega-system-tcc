# Demonstração com Docker

Este guia sobe o Adega System inteiro, **já com os dados de demonstração**, só com o Docker: sem instalar .NET SDK,
Node.js ou MySQL no computador. Depois do primeiro build, funciona **sem internet**, o que serve para apresentações.

Os dados são os mesmos do [DADOS-DEMO.md](DADOS-DEMO.md): a carga é o comando `seed-demo` que já existe na API,
rodando dentro de um contêiner, com todas as travas dele.

## Sumário

- [Visão geral](#visão-geral)
- [Requisitos](#requisitos)
- [Preparar (uma vez, com internet)](#preparar-uma-vez-com-internet)
- [Usar](#usar)
- [Recriar os dados (reset)](#recriar-os-dados-reset)
- [Sem internet](#sem-internet)
- [Separação da produção](#separação-da-produção)
- [Problemas comuns](#problemas-comuns)

## Visão geral

O `docker-compose.demo.yml` é um projeto Docker próprio, chamado `adega-demo`:

```
navegador ──► web (nginx, 127.0.0.1:8081) ──► /api, /health ──► api (use-demo) ──► mysql (adega_demo, sem porta exposta)
                                                                       ▲
                                    seed (seed-demo, roda uma vez e encerra) ─┘
```

| Serviço | Imagem | O que faz |
| --- | --- | --- |
| `mysql` | `mysql:9.7.2` | MySQL só da demo, com o banco `adega_demo` e o volume `adega-demo_mysql-data` |
| `seed` | `adega-demo-api:local` | Roda `seed-demo` em Development: migrations, senha do admin e carga. Encerra ao terminar |
| `api` | `adega-demo-api:local` | A API em Development com `use-demo`. Só sobe depois que o `seed` termina **com sucesso** |
| `web` | `adega-demo-web:local` | O frontend (nginx), igual ao de produção, que repassa `/api` para a API |

A ordem de subida é garantida pelo próprio compose: `mysql` saudável → `seed` termina com código 0 → `api` → `web`.
Se a carga falhar, a API não sobe e o erro aparece no log do `seed`.

As imagens `seed` e `api` são a **mesma** (`adega-demo-api:local`), construída a partir do mesmo `Dockerfile` da
produção: o código da demo já vai compilado dentro da API. Muda só o argumento (`seed-demo` ou `use-demo`) e o ambiente.

## Requisitos

- Docker Desktop (Windows/macOS) ou Docker Engine com Compose v2 (Linux).
- Cerca de 3 GB de disco para as imagens.
- Internet **só** no primeiro build.

## Preparar (uma vez, com internet)

1. Copie o modelo e preencha os valores (o `.env.demo` não vai para o Git):

   ```powershell
   Copy-Item .env.demo.example .env.demo
   ```

   | Variável | O que é |
   | --- | --- |
   | `MYSQL_ROOT_PASSWORD`, `MYSQL_PASSWORD` | Senhas do MySQL da demo (letras e números) |
   | `JWT_SECRET` | Chave dos tokens, 32+ caracteres |
   | `ADMIN_INITIAL_PASSWORD` | Senha do `admin@adega.local` na demo (8+ caracteres, letras e números) |
   | `DEMO_USERS_PASSWORD` | Senha dos 4 usuários demo (8+ caracteres, letras e números) |
   | `DEMO_PORT` | Porta da demo no computador (padrão 8081) |

   As senhas vêm **vazias** no modelo: enquanto não forem preenchidas, o `docker compose` se recusa a subir e avisa
   qual variável falta. Use valores próprios da demo, nunca os da produção.

2. Construa as imagens (baixa o .NET, o Node, o nginx, o MySQL e os pacotes; demora alguns minutos):

   ```powershell
   docker compose -f docker-compose.demo.yml --env-file .env.demo build
   ```

3. Suba uma vez para conferir:

   ```powershell
   docker compose -f docker-compose.demo.yml --env-file .env.demo up -d
   ```

   A primeira subida leva cerca de um minuto (o MySQL cria o banco e o `seed` carrega os dados).

## Usar

| Ação | Comando |
| --- | --- |
| Iniciar | `docker compose -f docker-compose.demo.yml --env-file .env.demo up -d` |
| Ver o estado | `docker compose -f docker-compose.demo.yml --env-file .env.demo ps -a` |
| Ver a carga | `docker compose -f docker-compose.demo.yml --env-file .env.demo logs seed` |
| Parar (mantém os dados) | `docker compose -f docker-compose.demo.yml --env-file .env.demo down` |

Acesse **http://localhost:8081**. `http://localhost:8081/health` responde `Healthy` quando a API e o banco estão no ar.

| Perfil | Login | Senha |
| --- | --- | --- |
| ADMIN | `admin@adega.local` | a de `ADMIN_INITIAL_PASSWORD` |
| VENDEDOR | `vendedor1@adegasystem.local`, `vendedor2@adegasystem.local` | a de `DEMO_USERS_PASSWORD` |
| ESTOQUISTA | `estoquista1@adegasystem.local`, `estoquista2@adegasystem.local` | a de `DEMO_USERS_PASSWORD` |

Rodar o `up -d` de novo não duplica nada: o `seed` vê que os dados já estão carregados, não altera o banco e
termina com sucesso. O que você cadastrou durante a demonstração continua lá até o reset.

## Recriar os dados (reset)

As datas da demo são **relativas ao dia da carga** (vendas de "hoje", lotes "vencendo em 7 dias"). Antes de uma
apresentação, recrie os dados para que tudo conte a partir do dia certo:

```powershell
docker compose -f docker-compose.demo.yml --env-file .env.demo down -v
docker compose -f docker-compose.demo.yml --env-file .env.demo up -d
```

O `down -v` apaga **só** o volume do projeto `adega-demo` (`adega-demo_mysql-data`). A produção usa outro projeto e
outro volume (`adega-prod_mysql-data`) e não é afetada. O reset funciona sem internet.

## Sem internet

Depois do build, nada no `up` depende da rede:

- As imagens da API e do frontend são locais (`adega-demo-api:local`, `adega-demo-web:local`) e têm
  `pull_policy: never`: o Docker nunca tenta baixá-las de um registro.
- O MySQL (`mysql:9.7.2`) fica no cache do Docker depois do primeiro build/up.
- Os dados demo estão compilados na API; o frontend não usa CDN (o `.wasm` do leitor de código de barras vai no build).

Para apresentar sem internet:

1. **Não** use `--build` nem `docker compose pull` no dia: eles precisam da rede.
2. Faça o teste antes: desligue o Wi-Fi, rode o reset completo (`down -v` e `up -d`) e navegue pelo sistema.
3. Depois de mudar o código (um `git pull`, por exemplo), rode o `build` de novo **com internet**.

Não apague as imagens no Docker Desktop (nem `docker system prune -a`) antes da apresentação: sem elas, o `up` não
consegue reconstruí-las sem internet.

## Separação da produção

| | Demonstração | Produção |
| --- | --- | --- |
| Arquivo | `docker-compose.demo.yml` + `.env.demo` | `docker-compose.prod.yml` + `.env.prod` |
| Projeto Docker | `adega-demo` | `adega-prod` |
| MySQL / banco | contêiner próprio / `adega_demo` | contêiner próprio / `adega` |
| Volume | `adega-demo_mysql-data` | `adega-prod_mysql-data` |
| Ambiente da API | Development (`use-demo`) | Production |
| Porta | `127.0.0.1:8081` (só o próprio computador) | `8080` |

As proteções da demo continuam valendo dentro do Docker, sem nenhuma exceção:

- `seed-demo` e `use-demo` recusam qualquer ambiente que não seja Development. O `docker-compose.prod.yml` continua em
  Production e não passa esses argumentos.
- A `DemoConnection` precisa apontar para um banco chamado exatamente `adega_demo` (o nome é fixo no compose), e a
  carga confere o banco aberto antes das migrations. A `DefaultConnection` nem é definida na demo.
- A carga só roda num banco sem dados e não duplica nada (marca `DemoDataSeededAt`).
- As senhas fracas são recusadas, e nenhuma senha fica no repositório.

Outros cuidados do ambiente demo:

- O MySQL e a API não têm porta exposta; só o nginx, e só em `127.0.0.1`.
- O Swagger do Development fica dentro do contêiner da API: o nginx só repassa `/api` e `/health`.
- O healthcheck do MySQL testa uma conexão de verdade (TCP, com o usuário `adega` no `adega_demo`), para a carga não
  começar enquanto o MySQL ainda reinicia depois de criar o banco.

## Problemas comuns

| Sintoma | Causa e solução |
| --- | --- |
| `defina ... no .env.demo` | Falta preencher uma variável no `.env.demo` |
| `api` não sobe e `seed` aparece como `Exited (1)` | A carga falhou. Veja `logs seed`. Se ela parou no meio, o próximo `up` recusa completar: faça o reset |
| `seed` diz que a senha é fraca | `DEMO_USERS_PASSWORD` ou `ADMIN_INITIAL_PASSWORD` precisa de 8+ caracteres, com letras e números |
| Trocou uma senha no `.env.demo` e nada mudou | As senhas valem na criação do banco. Faça o reset para aplicar |
| `No such image: adega-demo-api:local` | As imagens não foram construídas (ou foram apagadas). Rode o `build`, com internet |
| Porta 8081 ocupada | Mude `DEMO_PORT` no `.env.demo` |
