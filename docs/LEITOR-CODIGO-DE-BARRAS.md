# Leitor de código de barras pela câmera

Leitura do código de barras pela câmera do celular, tablet ou webcam, no **cadastro/edição de produto**, no **PDV**,
na **pesquisa de Produtos** e na **consulta rápida** da tela inicial.
O leitor só captura o número: não consulta catálogos externos, não grava vídeo, não salva fotos e não envia
imagens para o backend.

## Como funciona

```
clique em "Escanear"
        ↓
BarcodeScannerModal ── abre ──► BarcodeScanner (câmera ligada só enquanto ele existe)
        ↓                               │ getUserMedia (câmera traseira preferida, sem áudio)
        ↓                               │ a cada 150 ms: lê o quadro atual do <video>
        ↓                               ▼
        ◄──────────── código lido (uma vez só; a câmera é desligada antes de avisar)
        ↓
Cadastro:  preenche o campo "Código de barras" (continua editável)
PDV:       findProductByBarcode → addToCart(produto), o mesmo caminho de um clique na lista
Produtos:  findProductByBarcode → a busca da lista vira o código salvo do produto
Consulta:  findProductByBarcode → ProductQuickViewModal (preço, estoque, lotes)
```

`findProductByBarcode` (`components/barcode/productLookup.ts`) chama `GET /api/products/barcode/{código}` e devolve
`found`, `not-found` ou `error`. A equivalência UPC-A/EAN-13 fica só no backend.

| Arquivo | Papel |
| --- | --- |
| `frontend/src/components/barcode/camera.ts` | Abre e desliga a câmera; traduz os erros do navegador em mensagens amigáveis |
| `frontend/src/components/barcode/barcodeReader.ts` | Escolhe o leitor: API nativa `BarcodeDetector` ou ZXing em WebAssembly |
| `frontend/src/components/barcode/BarcodeScanner.tsx` | Vídeo + loop de leitura + estados (iniciando, lendo, erro) |
| `frontend/src/components/barcode/BarcodeScannerModal.tsx` | Modal responsivo (tela cheia no celular) |
| `frontend/src/components/barcode/productLookup.ts` | `findProductByBarcode` e o caminho do cadastro com código (`/produtos?novo=1&codigo=...`) |
| `frontend/src/components/barcode/ProductNotFoundAlert.tsx` | "Produto não encontrado." + "Cadastrar produto" só para ADMIN e ESTOQUISTA |
| `frontend/src/components/barcode/QuickProductLookup.tsx` | Botão "Consultar produto" da tela inicial |
| `frontend/src/components/barcode/ProductQuickViewModal.tsx` | Modal da consulta rápida |
| `backend/src/AdegaSystem.Api/Common/Gtin.cs` | Equivalência UPC-A ⇄ EAN-13 e dígito verificador |

Formatos lidos: **EAN-13, EAN-8, UPC-A e UPC-E** (QR Code e outros ficam de fora para evitar leituras erradas).

## Biblioteca: `barcode-detector`

- Quando o navegador tem a [Barcode Detection API](https://wicg.github.io/shape-detection-api/#barcode-detection-api)
  nativa e ela lê os quatro formatos (ex.: Chrome no Android), usamos a nativa.
- Quando não tem (Safari/iPhone, Firefox, Chrome no Windows), usamos a biblioteca
  [`barcode-detector`](https://github.com/Sec-ant/barcode-detector), que implementa a mesma API com o
  ZXing-C++ compilado para WebAssembly (`zxing-wasm`). Assim a compatibilidade não depende da API nativa.
- A biblioteca é carregada **só quando o leitor abre** (import dinâmico), então não pesa no carregamento inicial.
- O arquivo `zxing_reader.wasm` (~1,1 MB, ~460 KB com gzip) vai **junto no build do Vite** (`/assets/`), em vez
  de ser baixado do CDN padrão da biblioteca: funciona sem acesso a terceiros e o nginx de produção já serve
  `.wasm` com o tipo correto.

Alternativas avaliadas: `@zxing/browser` (JavaScript puro, versão 0.2.x, leitura 1D em vídeo mais lenta),
`html5-qrcode` (sem atualização desde 2023) e `@yudiel/react-qr-scanner` (usa o próprio `barcode-detector`, mas
traz interface pronta e mais dependências).

## UPC-A × EAN-13

O mesmo produto pode ser lido como UPC-A (12 dígitos, ex.: `036000291452`) ou como EAN-13 com um 0 na frente
(`0036000291452`), dependendo do leitor. A busca por código (`GET /api/products/barcode/{barcode}`) trata isso assim:

1. Procura o **valor exato**.
2. Só se não achar, e só se o código tiver o **dígito verificador correto**, procura a forma equivalente
   (12 dígitos → `0` + código; 13 dígitos começando com `0` → sem o `0`).

Garantias:

- Os códigos salvos **não são reescritos**.
- O exato sempre ganha, e o índice único garante no máximo um produto por forma: dois produtos diferentes nunca
  são tratados como o mesmo.
- No cadastro e na edição, um código cuja forma equivalente já pertence a **outro** produto é recusado
  ("Já existe um produto com o código ..., que é o mesmo código de barras (UPC-A/EAN-13)."). Essa checagem só roda
  quando o código é novo ou mudou: se o banco já tiver um caso antigo assim, editar outros campos continua funcionando.
- Trocar o código do próprio produto pela forma equivalente é permitido.
- UPC-E (8 dígitos) não é convertido para UPC-A: fica como limitação conhecida.

Sem migration nova: o índice único em `Products.Barcode` já existe desde a Fase 1 (o MySQL aceita vários `NULL`,
por isso código vazio vira `null`).

## PDV

- O botão **Escanear** fica ao lado da busca (só na aba Produtos).
- Produto encontrado e ativo: vai para o carrinho pelo `addToCart` de sempre. Ler de novo soma 1 na quantidade;
  sem estoque suficiente, aparece o mesmo aviso de estoque e nada muda. Preço, lotes e FEFO continuam sendo
  decididos pela API ao finalizar.
- Produto inativo: aviso, não entra no carrinho.
- Código não cadastrado: **"Produto não encontrado."** Para quem pode cadastrar produtos (ADMIN; o ESTOQUISTA não
  acessa o PDV), aparece **Cadastrar produto**, que abre `/produtos?novo=1&codigo=...` com o código preenchido. Com
  itens no carrinho, o sistema pede confirmação antes (sair do PDV descarta o carrinho). A tela de Produtos só abre
  o formulário para quem tem permissão, e a API recusa o cadastro de quem não tem (403).
- O leitor USB (que "digita" o código e aperta Enter) continua funcionando na busca, e também se beneficia da
  equivalência UPC-A/EAN-13.

## Pesquisa de Produtos

- Botão **Escanear código** no fim do campo de busca (só o ícone em telas menores).
- Produto encontrado: a busca é preenchida com o **código salvo** do produto (que pode ser a outra forma UPC-A/EAN-13
  do código lido), os filtros de categoria e estoque são limpos e, se o produto estiver inativo, a situação vai para
  "Todos". A lista mostra o produto na própria tela, com a mensagem "Produto encontrado: ...".
- Código não cadastrado: "Produto não encontrado.". ADMIN e ESTOQUISTA veem **Cadastrar produto**, que abre o mesmo
  formulário de cadastro já com o código. O VENDEDOR não vê o botão.
- A busca manual por nome, marca ou código continua igual.

## Consulta rápida (tela inicial)

- Botão **Consultar produto** no topo do Dashboard (ADMIN) e da tela de atalhos (VENDEDOR e ESTOQUISTA).
- Lê o código e abre um modal (tela cheia no celular) com:
  - **todos os perfis:** nome, marca e volume, **preço de venda** e **estoque atual** em destaque, situação do estoque
    (sem estoque / baixo / normal, escrita, não só pela cor), categoria, código de barras, estoque mínimo e aviso de
    produto inativo;
  - **só ADMIN e ESTOQUISTA:** os 3 próximos lotes com saldo (número, quantidade e validade) e o alerta "Há lote vencido
    com saldo", com link para a tela de Lotes. Vem de `GET /api/batches?productId=...&pageSize=3`, que já é restrito a
    esses perfis; para o VENDEDOR a requisição nem é feita.
- **Custo e margem não aparecem para ninguém** na consulta rápida.
- **Consultar outro** fecha o modal e abre o leitor de novo; **Fechar** (ou o X) encerra.
- Código não cadastrado: "Produto não encontrado." e, para ADMIN e ESTOQUISTA, **Cadastrar produto**
  (vai para `/produtos?novo=1&codigo=...`).
- Nenhum endpoint novo: tudo reaproveita a busca por código e a listagem de lotes.

## Câmera, HTTPS e privacidade

Os navegadores só liberam a câmera em **contexto seguro**:

| Endereço | Câmera |
| --- | --- |
| `http://localhost:5173` (no próprio computador) | Funciona |
| `http://192.168.x.x:5173` (celular na mesma rede, sem HTTPS) | **Bloqueada** pelo navegador: o leitor mostra a mensagem pedindo HTTPS |
| `https://...` (produção atrás de um proxy com certificado) | Funciona |

- Para testar no celular durante o desenvolvimento, use HTTPS (por exemplo, um túnel com certificado). Não use
  "flags" do navegador para tratar HTTP como seguro.
- Em produção, coloque o sistema atrás de HTTPS (ver "Deploy" no README). O nginx não envia `Permissions-Policy`
  que bloqueie a câmera.
- A câmera só é pedida depois do clique em **Escanear**, e é desligada ao ler, ao cancelar, ao fechar o modal e ao
  sair da tela. Nenhum quadro é guardado ou enviado; só o número lido vai para a API.

## Testes

- Backend (`dotnet test`): `GtinTests` (equivalência e dígito verificador), `ProductServiceTests` (busca exata,
  equivalente, prioridade do exato, código inválido, duplicado equivalente, edição mantendo o próprio código) e
  `AuthorizationTests` (endpoint de busca para os três perfis e 401 sem login).
- Frontend (`npm test`, Vitest + Testing Library + jsdom): câmera e leitor simulados, sem vídeo real.
  `BarcodeScanner.test.tsx`, `ProductFormModal.test.tsx`, `ProductsPage.test.tsx`, `PosPage.test.tsx` e
  `QuickProductLookup.test.tsx`.

## Roteiro de teste manual

Pré-requisitos: API e frontend rodando; produtos com estoque; uma embalagem com código EAN-13 e, se possível, uma
com UPC-A (produtos importados dos EUA).

**Desktop com webcam (Chrome/Edge em `http://localhost:5173`)**

1. Produtos → Novo produto → botão de código de barras. O navegador pede a câmera só agora. Permita.
2. Mostre o código para a webcam: o campo é preenchido, o modal fecha e a luz da webcam apaga.
3. Apague um dígito no campo e digite de novo: o campo continua editável. Salve.
4. Edite o mesmo produto e salve sem mudar o código: deve salvar normalmente.
5. Cadastre outro produto lendo o mesmo código: a API recusa com "Já existe um produto com esse código de barras.".
6. Abra o leitor e clique em **Cancelar** (e depois teste o X e a tecla Esc): o modal fecha, o formulário continua
   aberto com os dados, e a luz da webcam apaga.
7. Abra o leitor e saia da tela pelo menu: a luz da webcam apaga.

**PDV**

1. **Escanear** → leia um produto cadastrado: aparece "... adicionado ao carrinho." e ele entra com quantidade 1.
2. Leia de novo: a quantidade vai para 2.
3. Leia um produto com estoque 1 duas vezes: na segunda aparece o aviso de estoque e a quantidade fica 1.
4. Leia um código que não existe: "Produto não encontrado.".
   - Como ADMIN: o botão **Cadastrar produto** aparece. Com carrinho vazio, abre o cadastro com o código preenchido;
     com itens no carrinho, pede confirmação antes.
   - Como VENDEDOR: o botão não aparece.
5. Desative um produto e leia o código dele: aviso de produto inativo, nada entra no carrinho.
6. Finalize uma venda feita por leitura: lotes (FEFO), lucro e estoque iguais a uma venda feita por clique.
7. Se tiver uma embalagem UPC-A cadastrada com 12 dígitos, leia-a (alguns leitores devolvem 13 dígitos): o produto
   deve ser encontrado do mesmo jeito.

**Pesquisa de Produtos**

1. Produtos → **Escanear código** → leia um produto cadastrado: a busca mostra o código e a lista fica só com ele,
   com "Produto encontrado: ...".
2. Selecione "Estoque baixo" ou outra categoria e leia de novo: os filtros são limpos e o produto aparece.
3. Leia o código de um produto inativo: a situação muda para "Todos" e ele aparece com o selo de inativo.
4. Leia um código inexistente: "Produto não encontrado.". Como ESTOQUISTA/ADMIN, **Cadastrar produto** abre o formulário
   com o código; como VENDEDOR, o botão não aparece.
5. Digite um nome na busca: a pesquisa manual funciona como antes.

**Consulta rápida (tela inicial)**

1. Como ADMIN (Dashboard), VENDEDOR e ESTOQUISTA (atalhos): o botão **Consultar produto** aparece no topo.
2. Leia um produto: o modal mostra preço e estoque em destaque, situação, categoria, código e estoque mínimo, e
   **nenhum** custo ou margem.
3. Como ADMIN/ESTOQUISTA: aparecem os próximos lotes; com um lote vencido com saldo, aparece o alerta e o link leva à
   tela de Lotes. Como VENDEDOR: a seção de lotes não aparece.
4. **Consultar outro** reabre o leitor; **Fechar**, o X e a tecla Esc fecham o modal. A luz da câmera apaga depois da
   leitura e ao cancelar.
5. Leia um código inexistente: "Produto não encontrado."; **Cadastrar produto** só para ADMIN/ESTOQUISTA.
6. No celular: botão fácil de tocar, modal em tela cheia, preço e estoque legíveis, nos temas claro e escuro.

**Câmera negada / sem câmera**

1. Bloqueie a câmera nas permissões do site (ícone ao lado do endereço) e abra o leitor: mensagem explicando como
   liberar e botão **Tentar de novo**.
2. Num computador sem webcam: "Nenhuma câmera foi encontrada neste dispositivo.".
3. Com a câmera aberta em outro aplicativo (Windows): mensagem de câmera em uso.

**Celular Android (Chrome) e iPhone (Safari)**, acessando por **HTTPS**:

1. O modal abre em tela cheia e usa a câmera traseira.
2. Repita os testes de cadastro e PDV acima.
3. Acesse por `http://IP-do-computador:5173`: o leitor deve explicar que precisa de HTTPS (sem pedir a câmera).
4. No iPhone, confirme que o vídeo aparece dentro da página (não abre em tela cheia do sistema) e que, ao fechar,
   o indicador de câmera do iOS some.

## Limitações conhecidas

- A câmera exige HTTPS fora do `localhost`.
- Uma leitura por abertura do leitor: para vários produtos seguidos no PDV, abra o leitor de novo (ou use leitor USB).
- Webcams sem foco automático leem mal códigos pequenos; o campo manual e o leitor USB continuam disponíveis.
- UPC-E não é convertido para UPC-A na busca.
- Em iPhones com iOS antigo a escolha da câmera traseira depende do navegador.
