import { useEffect, useState, type FormEvent } from 'react'
import { Alert, Button, Col, Form, Modal, Row } from 'react-bootstrap'
import { batchesApi } from '../../api/batches'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { stockApi } from '../../api/stock'
import { ProductPicker } from '../../components/ProductPicker'
import { RequiredMark } from '../../components/RequiredMark'
import type { Batch } from '../../types/batch'
import type { Product } from '../../types/product'
import { formatDate } from '../../utils/format'

/** Na tela o usuário escolhe o tipo e digita uma quantidade positiva; o sinal é definido aqui. */
type AdjustmentKind = 'LOSS' | 'ADJUSTMENT_IN' | 'ADJUSTMENT_OUT'

const kinds: { value: AdjustmentKind; label: string; help: string }[] = [
  { value: 'LOSS', label: 'Perda', help: 'Quebra, avaria ou consumo interno. Retira do estoque.' },
  { value: 'ADJUSTMENT_OUT', label: 'Correção: saída', help: 'A contagem física deu menos do que o sistema.' },
  { value: 'ADJUSTMENT_IN', label: 'Correção: entrada', help: 'A contagem física deu mais do que o sistema.' },
]

interface StockAdjustmentModalProps {
  onClose: () => void
  onSaved: (message: string) => void
}

export function StockAdjustmentModal({ onClose, onSaved }: StockAdjustmentModalProps) {
  const [product, setProduct] = useState<Product | null>(null)
  const [batches, setBatches] = useState<Batch[]>([])
  const [batchId, setBatchId] = useState('')
  const [kind, setKind] = useState<AdjustmentKind>('LOSS')
  const [quantity, setQuantity] = useState('1')
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  // Lotes do produto escolhido (inclusive esgotados, porque uma correção de entrada pode cair num deles).
  useEffect(() => {
    if (!product) return
    let ignore = false
    batchesApi
      .listByProduct(product.id, false)
      .then((data) => !ignore && setBatches(data))
      .catch((err) => !ignore && setError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [product])

  function chooseProduct(selected: Product | null) {
    setProduct(selected)
    setBatches([])
    setBatchId('')
  }

  const amount = Number(quantity) || 0
  const signedQuantity = kind === 'ADJUSTMENT_IN' ? amount : -amount
  // Saídas só fazem sentido em lotes com saldo.
  const batchOptions = kind === 'ADJUSTMENT_IN' ? batches : batches.filter((b) => b.quantityAvailable > 0)
  const batch = batches.find((b) => String(b.id) === batchId) ?? null
  const newBatchQuantity = batch ? batch.quantityAvailable + signedQuantity : null

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!product || !batch) {
      setFieldErrors(product ? { batchId: 'Selecione o lote.' } : { productId: 'Selecione o produto.' })
      return
    }
    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      const movement = await stockApi.adjust({
        batchId: batch.id,
        type: kind === 'LOSS' ? 'LOSS' : 'ADJUSTMENT',
        quantity: signedQuantity,
        reason,
      })
      onSaved(
        `Estoque de "${movement.productName}" (lote ${movement.batchNumber}) ajustado: ${movement.previousStock} → ${movement.newStock}.`,
      )
    } catch (err) {
      setError(getErrorMessage(err))
      setFieldErrors(getFieldErrors(err))
      setSaving(false)
    }
  }

  return (
    <Modal show onHide={saving ? undefined : onClose} backdrop="static" keyboard={false} centered>
      <Form onSubmit={handleSubmit} noValidate>
        <Modal.Header closeButton closeLabel="Fechar">
          <Modal.Title as="h5">Ajustar estoque</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" className="py-2">{error}</Alert>}
          <Row className="g-3">
            <Col xs={12}>
              <Form.Label htmlFor="adjust-product">Produto<RequiredMark /></Form.Label>
              {product ? (
                <div className="d-flex justify-content-between align-items-center border rounded px-3 py-2">
                  <span>
                    <span className="fw-medium">{product.name}</span>{' '}
                    <small className="text-secondary">estoque atual {product.currentStock}</small>
                  </span>
                  <Button size="sm" variant="link" onClick={() => chooseProduct(null)}>
                    Trocar
                  </Button>
                </div>
              ) : (
                <ProductPicker id="adjust-product" autoFocus onSelect={chooseProduct} isInvalid={!!fieldErrors.productId} />
              )}
              {fieldErrors.productId && <div className="invalid-feedback d-block">{fieldErrors.productId}</div>}
            </Col>
            {product && (
              <Col xs={12}>
                <Form.Group controlId="adjust-batch">
                  <Form.Label>Lote<RequiredMark /></Form.Label>
                  <Form.Select value={batchId} onChange={(e) => setBatchId(e.target.value)} isInvalid={!!fieldErrors.batchId}>
                    <option value="">Selecione...</option>
                    {batchOptions.map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.batchNumber} · {b.expirationDate ? `validade ${formatDate(b.expirationDate)}` : 'sem validade'} · saldo{' '}
                        {b.quantityAvailable}
                      </option>
                    ))}
                  </Form.Select>
                  <Form.Control.Feedback type="invalid">{fieldErrors.batchId}</Form.Control.Feedback>
                  {batchOptions.length === 0 && (
                    <Form.Text>Este produto não tem lotes com saldo. Entradas de estoque são feitas pelas compras.</Form.Text>
                  )}
                </Form.Group>
              </Col>
            )}
            <Col xs={12}>
              <Form.Label>Tipo<RequiredMark /></Form.Label>
              {kinds.map((k) => (
                <Form.Check
                  key={k.value}
                  type="radio"
                  id={`adjust-kind-${k.value}`}
                  name="adjust-kind"
                  checked={kind === k.value}
                  onChange={() => setKind(k.value)}
                  label={
                    <>
                      {k.label} <small className="text-secondary">· {k.help}</small>
                    </>
                  }
                />
              ))}
            </Col>
            <Col xs={5}>
              <Form.Group controlId="adjust-quantity">
                <Form.Label>Quantidade<RequiredMark /></Form.Label>
                <Form.Control
                  type="number"
                  min={1}
                  value={quantity}
                  onChange={(e) => setQuantity(e.target.value)}
                  isInvalid={!!fieldErrors.quantity}
                />
                <Form.Control.Feedback type="invalid">{fieldErrors.quantity}</Form.Control.Feedback>
              </Form.Group>
            </Col>
            <Col xs={7} className="d-flex align-items-end">
              {newBatchQuantity !== null && (
                <small className={newBatchQuantity < 0 ? 'text-danger' : 'text-secondary'}>
                  Saldo do lote após o ajuste: {newBatchQuantity}
                  {newBatchQuantity < 0 && ' (insuficiente)'}
                </small>
              )}
            </Col>
            <Col xs={12}>
              <Form.Group controlId="adjust-reason">
                <Form.Label>Motivo<RequiredMark /></Form.Label>
                <Form.Control
                  as="textarea"
                  rows={2}
                  maxLength={255}
                  placeholder="Ex.: 2 garrafas quebradas no transporte"
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  isInvalid={!!fieldErrors.reason}
                />
                <Form.Control.Feedback type="invalid">{fieldErrors.reason}</Form.Control.Feedback>
              </Form.Group>
            </Col>
          </Row>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="outline-secondary" onClick={onClose} disabled={saving}>
            Cancelar
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Salvando...' : 'Registrar ajuste'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  )
}
