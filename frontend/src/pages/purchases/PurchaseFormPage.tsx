import { useEffect, useState, type FormEvent } from 'react'
import { Alert, Button, Card, Col, Form, Row, Table } from 'react-bootstrap'
import { useNavigate } from 'react-router'
import { categoriesApi } from '../../api/categories'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { purchasesApi } from '../../api/purchases'
import { suppliersApi } from '../../api/suppliers'
import { access } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { ProductPicker } from '../../components/ProductPicker'
import { RequiredMark } from '../../components/RequiredMark'
import { useMinimumMargin } from '../../hooks/useMinimumMargin'
import type { Category } from '../../types/category'
import type { Product } from '../../types/product'
import type { PurchaseInput } from '../../types/purchase'
import type { Supplier } from '../../types/supplier'
import { emptyToNull, formatCurrency, parseDecimal, todayIso } from '../../utils/format'
import { ProductFormModal } from '../products/ProductFormModal'

// Uma linha da compra na tela. Cada linha vira um lote. Os campos ficam como texto até o envio.
interface ItemRow {
  /** Identificador só da tela: o mesmo produto pode aparecer em várias linhas (um lote em cada). */
  key: number
  product: Product
  quantity: string
  unitCost: string
  batchNumber: string
  manufacturingDate: string
  expirationDate: string
}

let nextKey = 1

/** Mesmo cálculo do backend: quantidade x custo, arredondado em 2 casas. O valor oficial vem da API. */
const lineTotal = (row: ItemRow) => {
  const total = Number(row.quantity) * parseDecimal(row.unitCost)
  return Number.isFinite(total) ? Math.round(total * 100) / 100 : 0
}

export function PurchaseFormPage() {
  const navigate = useNavigate()
  const { hasRole } = useAuth()
  // Compras são do ADMIN e do ESTOQUISTA, que também podem cadastrar produto; a checagem deixa isso explícito.
  const canCreateProduct = hasRole(access.catalogWrite)
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [supplierId, setSupplierId] = useState('')
  const [invoiceNumber, setInvoiceNumber] = useState('')
  const [purchaseDate, setPurchaseDate] = useState(todayIso)
  const [items, setItems] = useState<ItemRow[]>([])

  const [saving, setSaving] = useState(false)
  const [confirmingCancel, setConfirmingCancel] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  // Cadastro rápido de produto: o mesmo formulário (e a mesma API) da tela de Produtos, num modal.
  // newProductName guarda o texto buscado, que já vem como nome; null = modal fechado.
  const [newProductName, setNewProductName] = useState<string | null>(null)
  const [categories, setCategories] = useState<Category[] | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const minimumMargin = useMinimumMargin()

  useEffect(() => {
    suppliersApi
      .listActive()
      .then(setSuppliers)
      .catch((err) => setError(getErrorMessage(err)))
  }, [])

  // O custo sugerido é o custo atual do produto; o usuário troca pelo valor da nota.
  const addProduct = (product: Product) =>
    setItems((rows) => [
      ...rows,
      {
        key: nextKey++,
        product,
        quantity: '1',
        unitCost: String(product.costPrice),
        batchNumber: '',
        manufacturingDate: '',
        expirationDate: '',
      },
    ])

  const updateItem = (index: number, changes: Partial<ItemRow>) =>
    setItems((rows) => rows.map((row, i) => (i === index ? { ...row, ...changes } : row)))

  const removeItem = (index: number) => setItems((rows) => rows.filter((_, i) => i !== index))

  /** Abre o cadastro rápido. As categorias só são buscadas na primeira vez que alguém precisa delas. */
  function openNewProduct(name: string) {
    setNotice(null)
    setNewProductName(name)
    if (categories === null) {
      categoriesApi
        .listActive()
        .then(setCategories)
        .catch((err) => {
          setNewProductName(null)
          setError(getErrorMessage(err))
        })
    }
  }

  /** Produto cadastrado: fecha o modal e já coloca o produto como uma linha nova, sem mexer nas outras. */
  function handleProductCreated(product: Product) {
    setNewProductName(null)
    addProduct(product)
    setNotice(`Produto "${product.name}" cadastrado e adicionado à compra.`)
  }

  const total = items.reduce((sum, row) => sum + lineTotal(row), 0)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})

    const input: PurchaseInput = {
      supplierId: Number(supplierId) || 0,
      invoiceNumber: emptyToNull(invoiceNumber),
      purchaseDate,
      items: items.map((row) => ({
        productId: row.product.id,
        quantity: Number(row.quantity) || 0,
        unitCost: parseDecimal(row.unitCost) || 0,
        batchNumber: emptyToNull(row.batchNumber),
        manufacturingDate: row.manufacturingDate || null,
        expirationDate: row.expirationDate || null,
      })),
    }

    try {
      const purchase = await purchasesApi.create(input)
      navigate('/compras', { state: { message: `Compra #${purchase.id} registrada. O estoque foi atualizado.` } })
    } catch (err) {
      setError(getErrorMessage(err))
      setFieldErrors(getFieldErrors(err))
      setSaving(false)
    }
  }

  // Erros dos itens chegam como "items[0].quantity".
  const itemError = (index: number, field: string) => fieldErrors[`items[${index}].${field}`]

  return (
    <>
      <h1 className="h4 mb-3">Nova compra</h1>

      <Form onSubmit={handleSubmit} noValidate>
        {error && <Alert variant="danger">{error}</Alert>}
        {notice && (
          <Alert variant="success" dismissible closeLabel="Fechar" onClose={() => setNotice(null)}>
            {notice}
          </Alert>
        )}

        <Card className="mb-3">
          <Card.Body>
            <Row className="g-3">
              <Col md={6}>
                <Form.Group controlId="purchase-supplier">
                  <Form.Label>Fornecedor<RequiredMark /></Form.Label>
                  <Form.Select
                    value={supplierId}
                    onChange={(e) => setSupplierId(e.target.value)}
                    isInvalid={!!fieldErrors.supplierId}
                  >
                    <option value="">Selecione...</option>
                    {suppliers.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.name}
                      </option>
                    ))}
                  </Form.Select>
                  <Form.Control.Feedback type="invalid">{fieldErrors.supplierId}</Form.Control.Feedback>
                </Form.Group>
              </Col>
              <Col sm={6} md={3}>
                <Form.Group controlId="purchase-invoice">
                  <Form.Label>Nota fiscal</Form.Label>
                  <Form.Control
                    value={invoiceNumber}
                    maxLength={50}
                    onChange={(e) => setInvoiceNumber(e.target.value)}
                    isInvalid={!!fieldErrors.invoiceNumber}
                  />
                  <Form.Control.Feedback type="invalid">{fieldErrors.invoiceNumber}</Form.Control.Feedback>
                </Form.Group>
              </Col>
              <Col sm={6} md={3}>
                <Form.Group controlId="purchase-date">
                  <Form.Label>Data da compra<RequiredMark /></Form.Label>
                  <Form.Control
                    type="date"
                    value={purchaseDate}
                    max={todayIso()}
                    onChange={(e) => setPurchaseDate(e.target.value)}
                    isInvalid={!!fieldErrors.purchaseDate}
                  />
                  <Form.Control.Feedback type="invalid">{fieldErrors.purchaseDate}</Form.Control.Feedback>
                </Form.Group>
              </Col>
            </Row>
          </Card.Body>
        </Card>

        <Card className="mb-3">
          <Card.Body>
            <div className="d-flex flex-wrap justify-content-between align-items-baseline gap-2">
              <Form.Label htmlFor="purchase-product-picker">Adicionar produto</Form.Label>
              {canCreateProduct && (
                <Button variant="link" size="sm" className="p-0 mb-2" onClick={() => openNewProduct('')}>
                  <i className="bi bi-plus-circle me-1" aria-hidden="true" />
                  Cadastrar novo produto
                </Button>
              )}
            </div>
            <ProductPicker
              id="purchase-product-picker"
              activeOnly
              onSelect={addProduct}
              onCreateNew={canCreateProduct ? openNewProduct : undefined}
              isInvalid={!!fieldErrors.items}
            />
            {fieldErrors.items && <div className="invalid-feedback d-block">{fieldErrors.items}</div>}
            <Form.Text>
              Cada linha é um lote. Se o mesmo produto veio com duas validades, adicione-o duas vezes. Deixe a validade em
              branco para produtos que não vencem.
            </Form.Text>

            {items.length > 0 && (
              <Table responsive className="align-middle mt-3 mb-0">
                <thead>
                  <tr>
                    <th>Produto</th>
                    <th style={{ minWidth: 120 }}>Lote</th>
                    <th style={{ minWidth: 150 }}>Fabricação</th>
                    <th style={{ minWidth: 150 }}>Validade</th>
                    <th style={{ minWidth: 100 }}>Qtd.</th>
                    <th style={{ minWidth: 120 }}>Custo unit. (R$)</th>
                    <th className="text-end">Total</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {items.map((row, index) => (
                    <tr key={row.key}>
                      <td>
                        <div className="fw-medium">{row.product.name}</div>
                        <small className="text-secondary">
                          estoque {row.product.currentStock} · custo atual {formatCurrency(row.product.costPrice)}
                        </small>
                      </td>
                      <td>
                        <Form.Control
                          maxLength={50}
                          placeholder="automático"
                          aria-label={`Lote de ${row.product.name}`}
                          value={row.batchNumber}
                          onChange={(e) => updateItem(index, { batchNumber: e.target.value })}
                          isInvalid={!!itemError(index, 'batchNumber')}
                        />
                        <Form.Control.Feedback type="invalid">{itemError(index, 'batchNumber')}</Form.Control.Feedback>
                      </td>
                      <td>
                        <Form.Control
                          type="date"
                          aria-label={`Fabricação de ${row.product.name}`}
                          value={row.manufacturingDate}
                          onChange={(e) => updateItem(index, { manufacturingDate: e.target.value })}
                          isInvalid={!!itemError(index, 'manufacturingDate')}
                        />
                        <Form.Control.Feedback type="invalid">{itemError(index, 'manufacturingDate')}</Form.Control.Feedback>
                      </td>
                      <td>
                        <Form.Control
                          type="date"
                          aria-label={`Validade de ${row.product.name}`}
                          value={row.expirationDate}
                          onChange={(e) => updateItem(index, { expirationDate: e.target.value })}
                          isInvalid={!!itemError(index, 'expirationDate')}
                        />
                        <Form.Control.Feedback type="invalid">{itemError(index, 'expirationDate')}</Form.Control.Feedback>
                      </td>
                      <td>
                        <Form.Control
                          type="number"
                          min={1}
                          aria-label={`Quantidade de ${row.product.name}`}
                          value={row.quantity}
                          onChange={(e) => updateItem(index, { quantity: e.target.value })}
                          isInvalid={!!itemError(index, 'quantity')}
                        />
                        <Form.Control.Feedback type="invalid">{itemError(index, 'quantity')}</Form.Control.Feedback>
                      </td>
                      <td>
                        <Form.Control
                          inputMode="decimal"
                          aria-label={`Custo unitário de ${row.product.name}`}
                          value={row.unitCost}
                          onChange={(e) => updateItem(index, { unitCost: e.target.value })}
                          isInvalid={!!itemError(index, 'unitCost')}
                        />
                        <Form.Control.Feedback type="invalid">{itemError(index, 'unitCost')}</Form.Control.Feedback>
                      </td>
                      <td className="text-end">{formatCurrency(lineTotal(row))}</td>
                      <td className="text-end">
                        <Button
                          size="sm"
                          variant="outline-danger"
                          onClick={() => removeItem(index)}
                          aria-label={`Remover ${row.product.name}`}
                          title="Remover item"
                        >
                          <i className="bi bi-trash3" aria-hidden="true" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <th colSpan={6} className="text-end">
                      Total da compra
                    </th>
                    <th className="text-end">{formatCurrency(total)}</th>
                    <th />
                  </tr>
                </tfoot>
              </Table>
            )}
          </Card.Body>
        </Card>

        <div className="d-flex justify-content-end gap-2">
          {/* Com itens lançados, pergunta antes de jogar o trabalho fora. */}
          <Button
            variant="outline-secondary"
            onClick={() => (items.length > 0 ? setConfirmingCancel(true) : navigate('/compras'))}
            disabled={saving}
          >
            Cancelar
          </Button>
          <Button type="submit" disabled={saving || items.length === 0}>
            {saving ? 'Registrando...' : 'Registrar compra'}
          </Button>
        </div>
      </Form>

      {newProductName !== null && categories !== null && (
        <ProductFormModal
          product={null}
          categories={categories}
          minimumMargin={minimumMargin}
          initialName={newProductName}
          onClose={() => setNewProductName(null)}
          onSaved={(_, product) => handleProductCreated(product)}
        />
      )}

      <ConfirmDialog
        show={confirmingCancel}
        title="Descartar compra"
        message={`A compra tem ${items.length} item(ns) lançado(s) que ainda não foram registrados. Deseja descartar?`}
        confirmLabel="Descartar"
        onConfirm={() => navigate('/compras')}
        onCancel={() => setConfirmingCancel(false)}
      />
    </>
  )
}
