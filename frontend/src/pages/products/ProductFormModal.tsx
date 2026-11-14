import { useState, type FormEvent } from 'react'
import { Alert, Button, Col, Form, InputGroup, Modal, Row } from 'react-bootstrap'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { productsApi } from '../../api/products'
import { BarcodeScannerModal } from '../../components/barcode/BarcodeScannerModal'
import { FieldLabel, RequiredMark } from '../../components/RequiredMark'
import type { Category } from '../../types/category'
import type { Product, ProductInput } from '../../types/product'
import { emptyToNull, formatPercent } from '../../utils/format'

interface ProductFormModalProps {
  /** null = novo produto; preenchido = edição. */
  product: Product | null
  categories: Category[]
  /** Margem mínima configurada (%); null enquanto carrega. */
  minimumMargin: number | null
  /** Nome já preenchido num cadastro novo (ex.: o texto buscado na Nova compra). */
  initialName?: string
  /** Código de barras já preenchido num cadastro novo (ex.: lido no PDV e não encontrado). */
  initialBarcode?: string
  onClose: () => void
  /** Recebe também o produto salvo, como a API devolveu (a Nova compra usa para já adicionar na lista). */
  onSaved: (message: string, product: Product) => void
}

// Os campos do formulário ficam como texto (é o que o <input> devolve)
// e só viram número na hora de enviar para a API.
interface FormState {
  name: string
  description: string
  barcode: string
  brand: string
  categoryId: string
  volume: string
  alcoholContent: string
  costPrice: string
  salePrice: string
  minimumStock: string
  active: boolean
}

function toFormState(product: Product | null, initialName = '', initialBarcode = ''): FormState {
  return {
    name: product?.name ?? initialName,
    description: product?.description ?? '',
    barcode: product ? (product.barcode ?? '') : initialBarcode,
    brand: product?.brand ?? '',
    categoryId: product ? String(product.categoryId) : '',
    volume: product?.volume ?? '',
    alcoholContent: product?.alcoholContent?.toString() ?? '',
    costPrice: product?.costPrice.toString() ?? '',
    salePrice: product?.salePrice.toString() ?? '',
    minimumStock: product?.minimumStock.toString() ?? '0',
    active: product?.active ?? true,
  }
}

/** Aceita "3,90" e "3.90". */
const toNumber = (value: string) => Number(value.replace(',', '.'))

export function ProductFormModal({
  product,
  categories,
  minimumMargin,
  initialName,
  initialBarcode,
  onClose,
  onSaved,
}: ProductFormModalProps) {
  const [form, setForm] = useState<FormState>(() => toFormState(product, initialName, initialBarcode))
  const [saving, setSaving] = useState(false)
  const [scanning, setScanning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  const set = (field: keyof FormState) => (value: string | boolean) => setForm((f) => ({ ...f, [field]: value }))

  // O código lido entra no campo como se tivesse sido digitado (continua editável).
  // Um erro antigo de "código já existe" deixa de valer para o código novo.
  function handleScanned(code: string) {
    setScanning(false)
    set('barcode')(code)
    setFieldErrors((errors) => {
      const rest = { ...errors }
      delete rest.barcode
      return rest
    })
  }

  // Prévia da margem enquanto o usuário digita. O valor oficial vem do backend.
  const cost = toNumber(form.costPrice)
  const sale = toNumber(form.salePrice)
  const marginPreview = sale > 0 && !Number.isNaN(cost) ? ((sale - cost) / sale) * 100 : null
  const belowMinimum = marginPreview !== null && minimumMargin !== null && marginPreview < minimumMargin

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})

    const input: ProductInput = {
      name: form.name,
      description: emptyToNull(form.description),
      barcode: emptyToNull(form.barcode),
      brand: emptyToNull(form.brand),
      categoryId: Number(form.categoryId) || 0,
      volume: emptyToNull(form.volume),
      alcoholContent: form.alcoholContent.trim() === '' ? null : toNumber(form.alcoholContent),
      costPrice: toNumber(form.costPrice) || 0,
      salePrice: toNumber(form.salePrice) || 0,
      minimumStock: Number(form.minimumStock) || 0,
    }

    try {
      if (product) {
        const saved = await productsApi.update(product.id, { ...input, active: form.active })
        onSaved('Produto atualizado com sucesso.', saved)
      } else {
        const saved = await productsApi.create(input)
        onSaved('Produto cadastrado com sucesso.', saved)
      }
    } catch (err) {
      setError(getErrorMessage(err))
      setFieldErrors(getFieldErrors(err))
    } finally {
      setSaving(false)
    }
  }

  // Ao editar um produto cuja categoria foi desativada, ela ainda precisa aparecer no select.
  const categoryOptions =
    product && !categories.some((c) => c.id === product.categoryId)
      ? [...categories, { id: product.categoryId, name: `${product.categoryName} (inativa)` } as Category]
      : categories

  const field = (name: keyof FormState, label: string, props: Record<string, unknown> = {}) => (
    <Form.Group controlId={`product-${name}`}>
      <Form.Label>
        <FieldLabel text={label} />
      </Form.Label>
      <Form.Control
        value={form[name] as string}
        onChange={(e) => set(name)(e.target.value)}
        isInvalid={!!fieldErrors[name]}
        {...props}
      />
      <Form.Control.Feedback type="invalid">{fieldErrors[name]}</Form.Control.Feedback>
    </Form.Group>
  )

  return (
    <Modal show onHide={saving ? undefined : onClose} backdrop="static" keyboard={false} size="lg" centered>
      <Form onSubmit={handleSubmit} noValidate>
        <Modal.Header closeButton closeLabel="Fechar">
          <Modal.Title as="h5">{product ? 'Editar produto' : 'Novo produto'}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" className="py-2">{error}</Alert>}
          <Row className="g-3">
            <Col md={8}>{field('name', 'Nome *', { maxLength: 150, autoFocus: true })}</Col>
            <Col md={4}>
              <Form.Group controlId="product-categoryId">
                <Form.Label>Categoria<RequiredMark /></Form.Label>
                <Form.Select
                  value={form.categoryId}
                  onChange={(e) => set('categoryId')(e.target.value)}
                  isInvalid={!!fieldErrors.categoryId}
                >
                  <option value="">Selecione...</option>
                  {categoryOptions.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </Form.Select>
                <Form.Control.Feedback type="invalid">{fieldErrors.categoryId}</Form.Control.Feedback>
              </Form.Group>
            </Col>
            <Col md={4}>
              <Form.Group controlId="product-barcode">
                <Form.Label>Código de barras</Form.Label>
                <InputGroup hasValidation>
                  <Form.Control
                    value={form.barcode}
                    onChange={(e) => set('barcode')(e.target.value)}
                    isInvalid={!!fieldErrors.barcode}
                    maxLength={50}
                    inputMode="numeric"
                    autoComplete="off"
                  />
                  <Button
                    variant="outline-secondary"
                    onClick={() => setScanning(true)}
                    disabled={saving}
                    aria-label="Escanear código de barras"
                    title="Escanear código de barras com a câmera"
                  >
                    <i className="bi bi-upc-scan" aria-hidden="true" />
                  </Button>
                  <Form.Control.Feedback type="invalid">{fieldErrors.barcode}</Form.Control.Feedback>
                </InputGroup>
              </Form.Group>
            </Col>
            <Col md={4}>{field('brand', 'Marca', { maxLength: 100 })}</Col>
            <Col md={2}>{field('volume', 'Volume', { maxLength: 30, placeholder: '350ml' })}</Col>
            <Col md={2}>{field('alcoholContent', 'Teor (%)', { inputMode: 'decimal' })}</Col>
            <Col md={4}>{field('costPrice', 'Preço de custo (R$) *', { inputMode: 'decimal' })}</Col>
            <Col md={4}>{field('salePrice', 'Preço de venda (R$) *', { inputMode: 'decimal' })}</Col>
            <Col md={4}>{field('minimumStock', 'Estoque mínimo *', { type: 'number', min: 0 })}</Col>
            <Col xs={12}>
              <small className="text-secondary">
                Margem:{' '}
                <span className={belowMinimum ? 'text-danger fw-semibold' : undefined}>
                  {marginPreview === null ? '—' : formatPercent(marginPreview)}
                </span>
                {product && ` · Estoque atual: ${product.currentStock} (alterado só por compras, vendas e ajustes)`}
              </small>
              {belowMinimum && (
                <div className="small text-danger">
                  ⚠ Margem abaixo do mínimo configurado. ({formatPercent(minimumMargin)})
                </div>
              )}
            </Col>
            <Col xs={12}>{field('description', 'Descrição', { as: 'textarea', rows: 2, maxLength: 500 })}</Col>
            {product && (
              <Col xs={12}>
                <Form.Check
                  type="switch"
                  id="product-active"
                  label="Ativo"
                  checked={form.active}
                  onChange={(e) => set('active')(e.target.checked)}
                />
              </Col>
            )}
          </Row>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="outline-secondary" onClick={onClose} disabled={saving}>
            Cancelar
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Salvando...' : 'Salvar'}
          </Button>
        </Modal.Footer>
      </Form>
      <BarcodeScannerModal show={scanning} onDetected={handleScanned} onClose={() => setScanning(false)} />
    </Modal>
  )
}
