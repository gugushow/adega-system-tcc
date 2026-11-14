import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { Alert, Badge, Button, ButtonGroup, Card, Col, Form, InputGroup, ListGroup, Row } from 'react-bootstrap'
import { useNavigate } from 'react-router'
import { combosApi } from '../../api/combos'
import { customersApi } from '../../api/customers'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { productsApi } from '../../api/products'
import { salesApi } from '../../api/sales'
import { BarcodeScannerModal } from '../../components/barcode/BarcodeScannerModal'
import { findProductByBarcode, newProductPath } from '../../components/barcode/productLookup'
import { ProductNotFoundAlert } from '../../components/barcode/ProductNotFoundAlert'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { CustomerPicker } from '../../components/CustomerPicker'
import { EmptyState } from '../../components/EmptyState'
import { Loading } from '../../components/Loading'
import { useDebounce } from '../../hooks/useDebounce'
import type { Combo } from '../../types/combo'
import type { Customer } from '../../types/customer'
import type { LoyaltyStatement } from '../../types/loyalty'
import type { Product } from '../../types/product'
import { paymentMethodLabels, type PaymentMethod } from '../../types/sale'
import { formatCurrency, parseDecimal } from '../../utils/format'
import { CustomerFormModal } from '../customers/CustomerFormModal'
import { SaleDetailsModal } from './SaleDetailsModal'
import './PosPage.css'

interface CartItem {
  product: Product
  quantity: number
}

interface CartCombo {
  combo: Combo
  quantity: number
}

interface LastSale {
  id: number
  total: number
  loyaltyPoints: number
}

type Catalog = 'products' | 'combos'

/** Resultado da última leitura pela câmera, mostrado acima da lista. */
type ScanNotice =
  | { kind: 'added' | 'inactive'; code: string; productName: string }
  | { kind: 'not-found'; code: string }
  | { kind: 'error'; code: string; message: string }

const paymentIcons: Record<PaymentMethod, string> = {
  PIX: 'bi-qr-code',
  CASH: 'bi-cash',
  CREDIT_CARD: 'bi-credit-card',
  DEBIT_CARD: 'bi-credit-card-2-front',
}

/**
 * Confere se o carrinho cabe no estoque, somando o que o produto aparece avulso e dentro dos combos
 * (2 Heinekens avulsas + 1 combo com 6 = 8 unidades). Devolve a mensagem do primeiro produto que falta.
 * É só uma prévia para o vendedor: a API confere de novo, lote por lote, ao finalizar.
 */
function findShortage(cart: CartItem[], combos: CartCombo[]): string | null {
  const needed = new Map<number, { name: string; stock: number; quantity: number }>()
  const add = (id: number, name: string, stock: number, quantity: number) => {
    const current = needed.get(id)
    needed.set(id, { name, stock, quantity: (current?.quantity ?? 0) + quantity })
  }

  cart.forEach((i) => add(i.product.id, i.product.name, i.product.currentStock, i.quantity))
  combos.forEach((c) => c.combo.items.forEach((i) => add(i.productId, i.productName, i.currentStock, i.quantity * c.quantity)))

  for (const item of needed.values()) {
    if (item.quantity > item.stock) {
      return `"${item.name}" tem só ${item.stock} unidade(s) em estoque (o carrinho precisa de ${item.quantity}).`
    }
  }
  return null
}

/**
 * PDV (ponto de venda): produtos e combos à esquerda, carrinho à direita.
 * Os totais aqui são só uma prévia: quem calcula de verdade (preço, lotes FEFO, lucro, pontos) é a API.
 */
export function PosPage() {
  const navigate = useNavigate()
  const searchRef = useRef<HTMLInputElement>(null)

  // Produtos e combos
  const [catalog, setCatalog] = useState<Catalog>('products')
  const [text, setText] = useState('')
  const search = useDebounce(text.trim(), 300)
  const [products, setProducts] = useState<Product[] | null>(null)
  const [combos, setCombos] = useState<Combo[] | null>(null)

  // Carrinho e pagamento
  const [cart, setCart] = useState<CartItem[]>([])
  const [cartCombos, setCartCombos] = useState<CartCombo[]>([])
  const [customer, setCustomer] = useState<Customer | null>(null)
  const [loyalty, setLoyalty] = useState<LoyaltyStatement | null>(null)
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('PIX')
  const [discountText, setDiscountText] = useState('')

  const [creatingCustomer, setCreatingCustomer] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [catalogError, setCatalogError] = useState<string | null>(null)
  const [warning, setWarning] = useState<string | null>(null)
  const [lastSale, setLastSale] = useState<LastSale | null>(null)
  const [viewingSaleId, setViewingSaleId] = useState<number | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  // Leitor de código de barras pela câmera
  const [scanning, setScanning] = useState(false)
  const [scanNotice, setScanNotice] = useState<ScanNotice | null>(null)
  const [pendingNewProductCode, setPendingNewProductCode] = useState<string | null>(null)

  useEffect(() => {
    let ignore = false
    // Sem busca, mostra os primeiros produtos (ou combos) ativos em ordem alfabética.
    const query = { search: search || undefined, active: true, pageSize: 12 }
    const request =
      catalog === 'products'
        ? productsApi.list(query).then((data) => !ignore && setProducts(data.items))
        : combosApi.list(query).then((data) => !ignore && setCombos(data.items))
    request
      .then(() => !ignore && setCatalogError(null))
      .catch((err) => !ignore && setCatalogError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [catalog, search, reloadKey])

  // Saldo de pontos do cliente escolhido (pageSize 1: aqui só interessa o saldo, não o extrato).
  useEffect(() => {
    if (!customer) return
    let ignore = false
    customersApi
      .loyalty(customer.id, { pageSize: 1 })
      .then((data) => !ignore && setLoyalty(data))
      .catch(() => !ignore && setLoyalty(null))
    return () => {
      ignore = true
    }
  }, [customer])

  const subtotal =
    cart.reduce((sum, item) => sum + item.quantity * item.product.salePrice, 0) +
    cartCombos.reduce((sum, item) => sum + item.quantity * item.combo.salePrice, 0)
  const discount = discountText.trim() === '' ? 0 : parseDecimal(discountText)
  const discountInvalid = Number.isNaN(discount) || discount < 0 || discount > subtotal
  const total = Math.max(subtotal - (Number.isNaN(discount) ? 0 : discount), 0)
  const cartIsEmpty = cart.length === 0 && cartCombos.length === 0

  // Prévia dos pontos: a mesma regra da API (R$ X gastos = 1 ponto, sempre arredondando para baixo).
  const customerLoyalty = customer && loyalty?.customerId === customer.id ? loyalty : null
  const pointsPreview =
    customerLoyalty?.programEnabled && customerLoyalty.amountPerPoint > 0
      ? Math.floor(total / customerLoyalty.amountPerPoint)
      : 0

  const canFinish = !cartIsEmpty && !discountInvalid && !saving

  /** Aplica o carrinho novo só se couber no estoque; senão mantém o atual e avisa. Devolve se aplicou. */
  function updateCart(nextCart: CartItem[], nextCombos: CartCombo[]) {
    const shortage = findShortage(nextCart, nextCombos)
    setWarning(shortage)
    if (shortage) return false
    setCart(nextCart)
    setCartCombos(nextCombos)
    return true
  }

  function addToCart(product: Product) {
    setLastSale(null)
    setScanNotice(null)
    const inCart = cart.some((item) => item.product.id === product.id)
    return updateCart(
      inCart
        ? cart.map((item) => (item.product.id === product.id ? { ...item, quantity: item.quantity + 1 } : item))
        : [...cart, { product, quantity: 1 }],
      cartCombos,
    )
  }

  function addComboToCart(combo: Combo) {
    setLastSale(null)
    const inCart = cartCombos.some((item) => item.combo.id === combo.id)
    updateCart(
      cart,
      inCart
        ? cartCombos.map((item) => (item.combo.id === combo.id ? { ...item, quantity: item.quantity + 1 } : item))
        : [...cartCombos, { combo, quantity: 1 }],
    )
  }

  // Quantidade nunca fica abaixo de 1: para tirar do carrinho, use o botão remover.
  const changeQuantity = (productId: number, quantity: number) =>
    updateCart(
      cart.map((i) => (i.product.id === productId ? { ...i, quantity: Math.max(1, quantity || 1) } : i)),
      cartCombos,
    )

  const changeComboQuantity = (comboId: number, quantity: number) =>
    updateCart(
      cart,
      cartCombos.map((i) => (i.combo.id === comboId ? { ...i, quantity: Math.max(1, quantity || 1) } : i)),
    )

  function removeFromCart(productId: number) {
    setWarning(null)
    setCart((items) => items.filter((item) => item.product.id !== productId))
  }

  function removeComboFromCart(comboId: number) {
    setWarning(null)
    setCartCombos((items) => items.filter((item) => item.combo.id !== comboId))
  }

  function switchCatalog(next: Catalog) {
    setCatalog(next)
    setText('')
    searchRef.current?.focus()
  }

  /** Enter na busca: tenta o código de barras exato (leitor); se não achar e só houver um resultado, usa ele. */
  async function handleSearchKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key !== 'Enter') return
    event.preventDefault()
    const code = text.trim()
    if (!code) return

    if (catalog === 'combos') {
      if (combos?.length === 1) {
        addComboToCart(combos[0])
        setText('')
      }
      return
    }

    try {
      const product = await productsApi.getByBarcode(code)
      if (product.active) {
        addToCart(product)
        setText('')
        return
      }
    } catch {
      // Não é um código de barras cadastrado: segue para a lista de resultados.
    }
    if (products?.length === 1) {
      addToCart(products[0])
      setText('')
    }
  }

  /**
   * Código lido pela câmera: só troca a forma de achar o produto. Depois segue exatamente o caminho
   * de um clique na lista (addToCart): mesma checagem de estoque, mesmo incremento de quantidade.
   */
  async function handleScanned(code: string) {
    setScanning(false)
    setWarning(null)
    const result = await findProductByBarcode(code)
    if (result.kind !== 'found') {
      setScanNotice(result)
    } else if (!result.product.active) {
      setScanNotice({ kind: 'inactive', code, productName: result.product.name })
    } else if (addToCart(result.product)) {
      setScanNotice({ kind: 'added', code, productName: result.product.name })
    }
    searchRef.current?.focus()
  }

  // Sair do PDV para cadastrar descarta o carrinho: com itens, pergunta antes.
  const goToNewProduct = (code: string) => navigate(newProductPath(code))

  async function finishSale() {
    setSaving(true)
    setError(null)
    setWarning(null)
    try {
      const sale = await salesApi.create({
        customerId: customer?.id ?? null,
        paymentMethod,
        discount: Number.isNaN(discount) ? 0 : discount,
        items: cart.map((item) => ({ productId: item.product.id, quantity: item.quantity })),
        combos: cartCombos.map((item) => ({ comboId: item.combo.id, quantity: item.quantity })),
      })
      setLastSale({ id: sale.id, total: sale.total, loyaltyPoints: sale.loyaltyPoints })
      setCart([])
      setCartCombos([])
      setCustomer(null)
      setDiscountText('')
      setPaymentMethod('PIX')
      setText('')
      setReloadKey((key) => key + 1) // atualiza o estoque mostrado na lista de produtos e combos
      searchRef.current?.focus()
    } catch (err) {
      const fields = getFieldErrors(err)
      setError(fields.discount ?? getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  // Atalhos do caixa: F2 volta para a busca e F9 finaliza a venda. Ficam desligados com um modal aberto.
  useEffect(() => {
    function handleKeyDown(event: globalThis.KeyboardEvent) {
      if (creatingCustomer || viewingSaleId !== null || scanning || pendingNewProductCode !== null) return
      if (event.key === 'F2') {
        event.preventDefault()
        searchRef.current?.focus()
      } else if (event.key === 'F9' && canFinish) {
        event.preventDefault()
        finishSale()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  })

  return (
    <>
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <h1 className="h4 mb-0">PDV</h1>
        <div className="d-flex align-items-center gap-3">
          {/* Atalhos do caixa: só em tela grande, onde há teclado físico. */}
          <span className="pos-shortcuts d-none d-lg-inline">
            <kbd>F2</kbd> buscar · <kbd>F9</kbd> finalizar
          </span>
          <Button variant="outline-secondary" onClick={() => navigate('/vendas/historico')}>
            <i className="bi bi-receipt me-1" aria-hidden="true" />
            Histórico de vendas
          </Button>
        </div>
      </div>

      {lastSale && (
        <Alert
          variant="success"
          dismissible closeLabel="Fechar"
          onClose={() => setLastSale(null)}
          className="d-flex flex-wrap align-items-center gap-3"
        >
          <span>
            Venda #{lastSale.id} finalizada: <strong>{formatCurrency(lastSale.total)}</strong>.
            {lastSale.loyaltyPoints > 0 && <> O cliente ganhou {lastSale.loyaltyPoints} ponto(s).</>}
          </span>
          <Button size="sm" variant="outline-success" onClick={() => setViewingSaleId(lastSale.id)}>
            Ver detalhes
          </Button>
        </Alert>
      )}

      <Row className="g-3 align-items-start">
        <Col lg={7}>
          <Card>
            <Card.Body>
              <div className="mb-3">
                <ButtonGroup aria-label="Mostrar produtos ou combos">
                  <Button
                    variant={catalog === 'products' ? 'secondary' : 'outline-secondary'}
                    aria-pressed={catalog === 'products'}
                    onClick={() => switchCatalog('products')}
                  >
                    <i className="bi bi-box-seam me-1" aria-hidden="true" />
                    Produtos
                  </Button>
                  <Button
                    variant={catalog === 'combos' ? 'secondary' : 'outline-secondary'}
                    aria-pressed={catalog === 'combos'}
                    onClick={() => switchCatalog('combos')}
                  >
                    <i className="bi bi-gift me-1" aria-hidden="true" />
                    Combos
                  </Button>
                </ButtonGroup>
              </div>

              <InputGroup size="lg" className="pos-search mb-3">
                <InputGroup.Text aria-hidden>
                  <i className={`bi ${catalog === 'products' ? 'bi-upc-scan' : 'bi-search'}`} />
                </InputGroup.Text>
                <Form.Control
                  ref={searchRef}
                  type="search"
                  value={text}
                  autoFocus
                  autoComplete="off"
                  placeholder={
                    catalog === 'products' ? 'Nome ou código de barras (Enter adiciona)' : 'Buscar combo pelo nome'
                  }
                  aria-label={catalog === 'products' ? 'Buscar produto' : 'Buscar combo'}
                  onChange={(e) => setText(e.target.value)}
                  onKeyDown={handleSearchKeyDown}
                />
                {catalog === 'products' && (
                  <Button
                    variant="outline-secondary"
                    onClick={() => setScanning(true)}
                    aria-label="Escanear código de barras"
                    title="Ler o código de barras com a câmera"
                  >
                    <i className="bi bi-camera me-sm-1" aria-hidden="true" />
                    <span className="d-none d-sm-inline" aria-hidden="true">
                      Escanear
                    </span>
                  </Button>
                )}
              </InputGroup>
              {catalog === 'products' &&
                scanNotice &&
                (scanNotice.kind === 'not-found' ? (
                  <ProductNotFoundAlert
                    code={scanNotice.code}
                    onCreateProduct={(code) => (cartIsEmpty ? goToNewProduct(code) : setPendingNewProductCode(code))}
                    onClose={() => setScanNotice(null)}
                  />
                ) : (
                  <ScanNoticeAlert notice={scanNotice} onClose={() => setScanNotice(null)} />
                ))}
              {catalogError && <Alert variant="danger" className="py-2">{catalogError}</Alert>}
              {/* Sem busca a lista mostra só os 12 primeiros: avisa para ninguém achar que o catálogo acabou. */}
              {!search && ((catalog === 'products' && products?.length === 12) || (catalog === 'combos' && combos?.length === 12)) && (
                <p className="small text-secondary mb-2">Mostrando os 12 primeiros. Use a busca para encontrar os outros.</p>
              )}
              {catalog === 'products' ? (
                !products ? (
                  <Loading />
                ) : products.length === 0 ? (
                  <EmptyState
                    icon="bi-search"
                    title="Nenhum produto ativo encontrado"
                    description={search ? 'Confira o nome ou o código digitado.' : undefined}
                  />
                ) : (
                  <ListGroup className="pos-catalog">
                    {products.map((product) => (
                      <ListGroup.Item
                        key={product.id}
                        action
                        disabled={product.currentStock === 0}
                        onClick={() => addToCart(product)}
                        className="d-flex justify-content-between align-items-center gap-2"
                      >
                        <div className="min-w-0">
                          <div className="fw-medium">{product.name}</div>
                          <small className="text-secondary">
                            {product.categoryName}
                            {product.barcode && <span className="font-monospace"> · {product.barcode}</span>}
                          </small>
                        </div>
                        <div className="text-end text-nowrap">
                          <div className="fw-semibold">{formatCurrency(product.salePrice)}</div>
                          {product.currentStock === 0 ? (
                            <Badge bg="danger">Sem estoque</Badge>
                          ) : product.isLowStock ? (
                            <Badge bg="warning">estoque {product.currentStock} · baixo</Badge>
                          ) : (
                            <small className="text-secondary">estoque {product.currentStock}</small>
                          )}
                        </div>
                        <i className="bi bi-plus-circle pos-add-icon" aria-hidden="true" />
                      </ListGroup.Item>
                    ))}
                  </ListGroup>
                )
              ) : !combos ? (
                <Loading />
              ) : combos.length === 0 ? (
                <EmptyState icon="bi-search" title="Nenhum combo ativo encontrado" />
              ) : (
                <ListGroup className="pos-catalog">
                  {combos.map((combo) => {
                    // A API recusa vender um combo que ficou mais caro que os produtos separados.
                    const blocked = combo.availableQuantity === 0 || combo.savings < 0
                    return (
                      <ListGroup.Item
                        key={combo.id}
                        action
                        disabled={blocked}
                        onClick={() => addComboToCart(combo)}
                        className="d-flex justify-content-between align-items-center gap-2"
                      >
                        <div className="min-w-0">
                          <div className="fw-medium">{combo.name}</div>
                          <small className="text-secondary">
                            {combo.items.map((i) => `${i.quantity}x ${i.productName}`).join(' + ')}
                          </small>
                        </div>
                        <div className="text-end text-nowrap">
                          <div className="fw-semibold">{formatCurrency(combo.salePrice)}</div>
                          {combo.savings < 0 ? (
                            <Badge bg="danger">preço desatualizado</Badge>
                          ) : combo.availableQuantity === 0 ? (
                            <Badge bg="danger">Sem estoque</Badge>
                          ) : (
                            <small className="text-success">economia {formatCurrency(combo.savings)}</small>
                          )}
                        </div>
                        <i className="bi bi-plus-circle pos-add-icon" aria-hidden="true" />
                      </ListGroup.Item>
                    )
                  })}
                </ListGroup>
              )}
            </Card.Body>
          </Card>
        </Col>

        <Col lg={5}>
          {/* No desktop o carrinho fica preso na tela: a lista rola e o total com o botão de finalizar sempre aparecem. */}
          <Card className="pos-cart">
            <Card.Header className="d-flex align-items-center justify-content-between">
              <span className="fw-semibold">
                <i className="bi bi-cart3 me-2" aria-hidden="true" />
                Carrinho
              </span>
              {!cartIsEmpty && (
                <Badge bg="neutral">
                  {cart.length + cartCombos.length} {cart.length + cartCombos.length === 1 ? 'item' : 'itens'}
                </Badge>
              )}
            </Card.Header>
            <Card.Body className="pos-cart-body">
              {error && <Alert variant="danger" className="py-2">{error}</Alert>}
              {warning && <Alert variant="warning" className="py-2">{warning}</Alert>}

              {cartIsEmpty ? (
                <EmptyState
                  icon="bi-cart"
                  title="Carrinho vazio"
                  description="Clique num produto ou combo, ou passe o código de barras, para adicionar."
                />
              ) : (
                <div className="mb-3">
                    {cart.map((item) => (
                      <CartRow
                        key={`p${item.product.id}`}
                        name={item.product.name}
                        detail={`${formatCurrency(item.product.salePrice)} cada`}
                        unitPrice={item.product.salePrice}
                        quantity={item.quantity}
                        onChange={(quantity) => changeQuantity(item.product.id, quantity)}
                        onRemove={() => removeFromCart(item.product.id)}
                      />
                    ))}
                    {cartCombos.map((item) => (
                      <CartRow
                        key={`c${item.combo.id}`}
                        name={item.combo.name}
                        badge="Combo"
                        detail={item.combo.items.map((i) => `${i.quantity}x ${i.productName}`).join(' + ')}
                        unitPrice={item.combo.salePrice}
                        quantity={item.quantity}
                        onChange={(quantity) => changeComboQuantity(item.combo.id, quantity)}
                        onRemove={() => removeComboFromCart(item.combo.id)}
                      />
                    ))}
                </div>
              )}

              <div className="pos-section">
                <Form.Group>
                  <Form.Label htmlFor="pos-customer">Cliente</Form.Label>
                  <CustomerPicker value={customer} onChange={setCustomer} onCreateNew={() => setCreatingCustomer(true)} />
                  {customerLoyalty && (
                    <Form.Text as="div" data-testid="pos-loyalty">
                      <i className="bi bi-star me-1" aria-hidden="true" />
                      {customerLoyalty.programEnabled ? (
                        <>
                          Saldo: <strong>{customerLoyalty.balance} ponto(s)</strong>
                          {pointsPreview > 0 && <> · esta venda dá +{pointsPreview}</>}
                        </>
                      ) : (
                        <>Programa de fidelidade desligado.</>
                      )}
                    </Form.Text>
                  )}
                </Form.Group>
              </div>

              <div className="pos-section">
                <Form.Group>
                  <Form.Label as="div" id="pos-payment-label">Forma de pagamento</Form.Label>
                  <Row className="g-2" role="group" aria-labelledby="pos-payment-label">
                    {(Object.keys(paymentMethodLabels) as PaymentMethod[]).map((method) => (
                      <Col xs={6} key={method}>
                        <Button
                          className="w-100 pos-payment"
                          variant={paymentMethod === method ? 'secondary' : 'outline-secondary'}
                          onClick={() => setPaymentMethod(method)}
                          aria-pressed={paymentMethod === method}
                        >
                          <i className={`bi ${paymentIcons[method]} me-1`} aria-hidden="true" />
                          {paymentMethodLabels[method]}
                        </Button>
                      </Col>
                    ))}
                  </Row>
                </Form.Group>
              </div>

              <div className="pos-section">
                <Form.Group controlId="pos-discount">
                  <Form.Label>Desconto (R$)</Form.Label>
                  <Form.Control
                    inputMode="decimal"
                    placeholder="0,00"
                    value={discountText}
                    isInvalid={!cartIsEmpty && discountInvalid}
                    onChange={(e) => setDiscountText(e.target.value)}
                  />
                  <Form.Control.Feedback type="invalid">O desconto precisa ficar entre zero e o subtotal.</Form.Control.Feedback>
                </Form.Group>
              </div>
            </Card.Body>

            <Card.Footer className="pos-cart-footer">
              <div className="d-flex justify-content-between small text-secondary">
                <span>Subtotal</span>
                <span>{formatCurrency(subtotal)}</span>
              </div>
              {!Number.isNaN(discount) && discount > 0 && (
                <div className="d-flex justify-content-between small pos-discount-line">
                  <span>Desconto</span>
                  <span>− {formatCurrency(discount)}</span>
                </div>
              )}
              <div className="d-flex justify-content-between align-items-baseline pos-total" aria-live="polite">
                <span>Total</span>
                <span>{formatCurrency(total)}</span>
              </div>

              <Button
                variant="success"
                size="lg"
                className="w-100 pos-finish"
                disabled={!canFinish}
                onClick={finishSale}
              >
                <i className="bi bi-check2-circle me-2" aria-hidden="true" />
                {saving ? 'Finalizando...' : 'Finalizar venda'}
                {/* A dica do atalho só aparece em tela grande: no celular não há teclado físico. */}
                <kbd className="ms-2 d-none d-lg-inline">F9</kbd>
              </Button>
            </Card.Footer>
          </Card>
        </Col>
      </Row>

      {creatingCustomer && (
        <CustomerFormModal
          customer={null}
          onClose={() => setCreatingCustomer(false)}
          onSaved={(_, created) => {
            setCustomer(created)
            setCreatingCustomer(false)
          }}
        />
      )}

      {viewingSaleId !== null && (
        <SaleDetailsModal
          saleId={viewingSaleId}
          onClose={() => setViewingSaleId(null)}
          onCanceled={() => setReloadKey((key) => key + 1)}
        />
      )}

      <BarcodeScannerModal show={scanning} onDetected={handleScanned} onClose={() => setScanning(false)} />

      <ConfirmDialog
        show={pendingNewProductCode !== null}
        title="Cadastrar produto"
        message="Para cadastrar o produto você sai do PDV e o carrinho atual será descartado. Deseja continuar?"
        confirmLabel="Ir para o cadastro"
        onConfirm={() => pendingNewProductCode !== null && goToNewProduct(pendingNewProductCode)}
        onCancel={() => setPendingNewProductCode(null)}
      />
    </>
  )
}

interface ScanNoticeAlertProps {
  notice: Exclude<ScanNotice, { kind: 'not-found' }>
  onClose: () => void
}

/** Resultado da leitura pela câmera (o "não encontrado" usa o ProductNotFoundAlert, igual às outras telas). */
function ScanNoticeAlert({ notice, onClose }: ScanNoticeAlertProps) {
  const code = <span className="font-monospace">{notice.code}</span>
  const variant = notice.kind === 'added' ? 'success' : notice.kind === 'error' ? 'danger' : 'warning'
  return (
    <Alert
      variant={variant}
      dismissible
      closeLabel="Fechar"
      onClose={onClose}
      className="py-2 d-flex flex-wrap align-items-center gap-2"
      role={notice.kind === 'added' ? 'status' : 'alert'}
    >
      <span>
        {notice.kind === 'added' && (
          <>
            <i className="bi bi-check-circle me-1" aria-hidden="true" />
            <strong>{notice.productName}</strong> adicionado ao carrinho.
          </>
        )}
        {notice.kind === 'inactive' && (
          <>
            <strong>{notice.productName}</strong> ({code}) está inativo e não pode ser vendido.
          </>
        )}
        {notice.kind === 'error' && notice.message}
      </span>
    </Alert>
  )
}

interface CartRowProps {
  name: string
  badge?: string
  detail: string
  unitPrice: number
  quantity: number
  onChange: (quantity: number) => void
  onRemove: () => void
}

/**
 * Uma linha do carrinho (produto avulso ou combo), com os botões de quantidade.
 * Em tela estreita (celular) os controles descem para baixo do nome, em vez de espremer o nome.
 */
function CartRow({ name, badge, detail, unitPrice, quantity, onChange, onRemove }: CartRowProps) {
  return (
    <div className="d-flex flex-wrap align-items-center column-gap-2 row-gap-1 py-2 border-bottom">
      <div className="flex-grow-1" style={{ flexBasis: 180, minWidth: 0 }}>
        <div>
          {name}{' '}
          {badge && (
            <Badge bg="info">
              {badge}
            </Badge>
          )}
        </div>
        <small className="text-secondary">{detail}</small>
      </div>
      <div className="d-flex align-items-center gap-2 ms-auto">
        <InputGroup className="pos-qty">
          <Button
            variant="outline-secondary"
            aria-label={`Diminuir ${name}`}
            disabled={quantity <= 1}
            onClick={() => onChange(quantity - 1)}
          >
            <i className="bi bi-dash" aria-hidden="true" />
          </Button>
          <Form.Control
            type="number"
            min={1}
            aria-label={`Quantidade de ${name}`}
            value={quantity}
            onChange={(e) => onChange(Number(e.target.value))}
            className="text-center px-1"
          />
          <Button variant="outline-secondary" aria-label={`Aumentar ${name}`} onClick={() => onChange(quantity + 1)}>
            <i className="bi bi-plus" aria-hidden="true" />
          </Button>
        </InputGroup>
        <span className="text-end text-nowrap fw-medium" style={{ minWidth: 80 }}>
          {formatCurrency(quantity * unitPrice)}
        </span>
        <Button
          variant="link"
          className="pos-remove"
          aria-label={`Remover ${name}`}
          title="Remover do carrinho"
          onClick={onRemove}
        >
          <i className="bi bi-trash3" aria-hidden="true" />
        </Button>
      </div>
    </div>
  )
}
