import { useEffect, useState } from 'react'
import { Badge, Button, Card, Col, Form, Row, Table } from 'react-bootstrap'
import { getErrorMessage } from '../../api/http'
import { stockApi } from '../../api/stock'
import { AppAlert, type Feedback } from '../../components/AppAlert'
import { DateInput } from '../../components/DateInput'
import { EmptyState } from '../../components/EmptyState'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import { ProductPicker } from '../../components/ProductPicker'
import type { PagedResult } from '../../types/api'
import type { Product } from '../../types/product'
import { movementTypeLabels, type MovementType, type StockMovement } from '../../types/stock'
import { formatDateTime } from '../../utils/format'
import { StockAdjustmentModal } from './StockAdjustmentModal'

const typeColors: Record<MovementType, string> = {
  PURCHASE: 'success',
  SALE: 'primary',
  ADJUSTMENT: 'info',
  LOSS: 'danger',
  EXPIRED: 'warning',
  RETURN: 'secondary',
}

/** Documentos que vale mostrar como referência. Na baixa de vencidos o lote já aparece na coluna Lote. */
const referenceNames: Record<string, string> = { Purchase: 'Compra', Sale: 'Venda' }

/** "Purchase" + 15 → "Compra #15". Nulo quando não há documento ou quando o motivo já diz isso. */
function referenceLabel(movement: StockMovement) {
  const name = movement.referenceType ? referenceNames[movement.referenceType] : undefined
  if (!name || movement.referenceId === null) return null
  const label = `${name} #${movement.referenceId}`
  // "Cancelamento da venda #12" já diz qual é a venda: não repete "Venda #12" embaixo.
  return movement.reason.toLowerCase().includes(label.toLowerCase()) ? null : label
}

/** Extrato de movimentações de estoque: toda entrada e saída, de qualquer origem. */
export function StockPage() {
  const [product, setProduct] = useState<Product | null>(null)
  const [type, setType] = useState<MovementType | ''>('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)

  const [result, setResult] = useState<PagedResult<StockMovement> | null>(null)
  const [loading, setLoading] = useState(true)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [reloadKey, setReloadKey] = useState(0)
  const [adjusting, setAdjusting] = useState(false)

  useEffect(() => {
    let ignore = false
    stockApi
      .movements({
        productId: product?.id,
        type: type || undefined,
        from: from || undefined,
        to: to || undefined,
        page,
        pageSize: 15,
      })
      .then((data) => !ignore && setResult(data))
      .catch((err) => !ignore && setFeedback({ type: 'danger', message: getErrorMessage(err) }))
      .finally(() => !ignore && setLoading(false))
    return () => {
      ignore = true
    }
  }, [product, type, from, to, page, reloadKey])

  const changeFilter = (apply: () => void) => {
    apply()
    setPage(1)
  }

  function handleSaved(message: string) {
    setAdjusting(false)
    setFeedback({ type: 'success', message })
    setReloadKey((key) => key + 1)
  }

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h4 mb-0">Estoque</h1>
        <Button onClick={() => setAdjusting(true)}>
          <i className="bi bi-sliders me-1" aria-hidden="true" />
          Ajustar estoque
        </Button>
      </div>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3">
            <Col lg={4}>
              {product ? (
                <div className="d-flex justify-content-between align-items-center border rounded px-3 py-1">
                  <span>
                    <span className="fw-medium">{product.name}</span>{' '}
                    <small className="text-secondary">estoque {product.currentStock}</small>
                  </span>
                  <Button size="sm" variant="link" onClick={() => changeFilter(() => setProduct(null))}>
                    Limpar
                  </Button>
                </div>
              ) : (
                <ProductPicker
                  placeholder="Filtrar por produto"
                  onSelect={(selected) => changeFilter(() => setProduct(selected))}
                />
              )}
            </Col>
            <Col sm={4} lg={2}>
              <Form.Select
                aria-label="Tipo"
                value={type}
                onChange={(e) => changeFilter(() => setType(e.target.value as MovementType | ''))}
              >
                <option value="">Todos os tipos</option>
                {Object.entries(movementTypeLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </Form.Select>
            </Col>
            <Col xs={6} sm={4} lg={3}>
              <DateInput label="De" value={from} max={to} onChange={(value) => changeFilter(() => setFrom(value))} />
            </Col>
            <Col xs={6} sm={4} lg={3}>
              <DateInput label="Até" value={to} min={from} onChange={(value) => changeFilter(() => setTo(value))} />
            </Col>
          </Row>

          {loading && !result ? (
            <Loading />
          ) : result && result.items.length === 0 ? (
            <EmptyState
              title="Nenhuma movimentação encontrada"
              description="As movimentações aparecem aqui a cada compra, venda, cancelamento, ajuste, perda e baixa de lote."
            />
          ) : (
            result && (
              <>
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>Data</th>
                      <th>Produto</th>
                      <th className="d-none d-md-table-cell">Lote</th>
                      <th>Tipo</th>
                      <th className="text-end">Qtd.</th>
                      <th className="d-none d-md-table-cell text-end">Estoque</th>
                      <th className="d-none d-md-table-cell">Motivo</th>
                      <th className="d-none d-md-table-cell">Usuário</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((movement) => (
                      <tr key={movement.id}>
                        <td className="text-nowrap small">{formatDateTime(movement.createdAt)}</td>
                        <td>{movement.productName}</td>
                        <td className="d-none d-md-table-cell font-monospace small">{movement.batchNumber ?? '—'}</td>
                        <td>
                          <Badge bg={typeColors[movement.movementType]}>{movementTypeLabels[movement.movementType]}</Badge>
                        </td>
                        <td className={`text-end fw-medium ${movement.quantity > 0 ? 'text-success' : 'text-danger'}`}>
                          {movement.quantity > 0 ? `+${movement.quantity}` : movement.quantity}
                        </td>
                        <td className="d-none d-md-table-cell text-end text-nowrap text-secondary">
                          {movement.previousStock} → <span className="text-body">{movement.newStock}</span>
                        </td>
                        <td className="d-none d-md-table-cell">
                          {movement.reason}
                          {referenceLabel(movement) && (
                            <div>
                              <small className="text-secondary">{referenceLabel(movement)}</small>
                            </div>
                          )}
                        </td>
                        <td className="d-none d-md-table-cell small">{movement.userName}</td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
                <Pagination
                  page={result.page}
                  totalPages={result.totalPages}
                  totalItems={result.totalItems}
                  onPageChange={setPage}
                />
              </>
            )
          )}
        </Card.Body>
      </Card>

      {adjusting && <StockAdjustmentModal onClose={() => setAdjusting(false)} onSaved={handleSaved} />}
    </>
  )
}
