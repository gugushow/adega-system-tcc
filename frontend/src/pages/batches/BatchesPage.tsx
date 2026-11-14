import { useEffect, useState } from 'react'
import { Button, ButtonGroup, Card, Col, Row, Table } from 'react-bootstrap'
import { useSearchParams } from 'react-router'
import { batchesApi } from '../../api/batches'
import { getErrorMessage } from '../../api/http'
import { ActionButton } from '../../components/ActionButton'
import { AppAlert, type Feedback } from '../../components/AppAlert'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState } from '../../components/EmptyState'
import { ExpirationBadge } from '../../components/ExpirationBadge'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import { ProductPicker } from '../../components/ProductPicker'
import type { PagedResult } from '../../types/api'
import type { Batch, BatchQuery } from '../../types/batch'
import type { Product } from '../../types/product'
import { formatDate, formatUnitCost } from '../../utils/format'

/** As abas da tela. Cada uma vira um filtro da API. */
type View = 'all' | 'expired' | 'expiring7' | 'expiring30' | 'expiring60' | 'empty'

const views: { value: View; label: string; query: Partial<BatchQuery> }[] = [
  { value: 'all', label: 'Com saldo', query: {} },
  { value: 'expired', label: 'Vencidos', query: { status: 'Expired' } },
  { value: 'expiring7', label: 'Vencem em 7 dias', query: { status: 'Expiring', days: 7 } },
  { value: 'expiring30', label: '30 dias', query: { status: 'Expiring', days: 30 } },
  { value: 'expiring60', label: '60 dias', query: { status: 'Expiring', days: 60 } },
  { value: 'empty', label: 'Todos (inclui esgotados)', query: { onlyAvailable: false } },
]

/** Lotes e validade: consultas de vencidos e vencendo, e a baixa de lotes vencidos. */
export function BatchesPage() {
  // ?visao=expired|expiring30...: os alertas do dashboard abrem direto na aba certa.
  const [searchParams] = useSearchParams()
  const [view, setView] = useState<View>(() => {
    const value = searchParams.get('visao')
    return views.some((v) => v.value === value) ? (value as View) : 'all'
  })
  const [product, setProduct] = useState<Product | null>(null)
  const [page, setPage] = useState(1)

  const [result, setResult] = useState<PagedResult<Batch> | null>(null)
  const [loading, setLoading] = useState(true)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [toWriteOff, setToWriteOff] = useState<Batch | null>(null)
  const [writingOff, setWritingOff] = useState(false)

  useEffect(() => {
    let ignore = false
    const viewQuery = views.find((v) => v.value === view)!.query
    batchesApi
      .list({ ...viewQuery, productId: product?.id, page, pageSize: 15 })
      .then((data) => !ignore && setResult(data))
      .catch((err) => !ignore && setFeedback({ type: 'danger', message: getErrorMessage(err) }))
      .finally(() => !ignore && setLoading(false))
    return () => {
      ignore = true
    }
  }, [view, product, page, reloadKey])

  const changeFilter = (apply: () => void) => {
    apply()
    setPage(1)
  }

  async function handleWriteOff() {
    if (!toWriteOff) return
    setWritingOff(true)
    try {
      await batchesApi.writeOff(toWriteOff.id, null)
      setFeedback({
        type: 'success',
        message: `Baixa registrada: ${toWriteOff.quantityAvailable} unidade(s) de "${toWriteOff.productName}" (lote ${toWriteOff.batchNumber}).`,
      })
      setReloadKey((key) => key + 1)
    } catch (err) {
      setFeedback({ type: 'danger', message: getErrorMessage(err) })
    } finally {
      setWritingOff(false)
      setToWriteOff(null)
    }
  }

  return (
    <>
      <h1 className="h4 mb-3">Lotes e validade</h1>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3 align-items-center">
            <Col xl={8}>
              <ButtonGroup className="flex-wrap" aria-label="Filtro de validade">
                {views.map((v) => (
                  <Button
                    key={v.value}
                    size="sm"
                    variant={view === v.value ? (v.value === 'expired' ? 'danger' : 'primary') : 'outline-secondary'}
                    onClick={() => changeFilter(() => setView(v.value))}
                  >
                    {v.label}
                  </Button>
                ))}
              </ButtonGroup>
            </Col>
            <Col xl={4}>
              {product ? (
                <div className="d-flex justify-content-between align-items-center border rounded px-3 py-1">
                  <span className="fw-medium">{product.name}</span>
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
          </Row>

          {loading && !result ? (
            <Loading />
          ) : result && result.items.length === 0 ? (
            <EmptyState
              title="Nenhum lote encontrado"
              description={view === 'expired' ? 'Nenhum lote vencido com saldo. Ótimo!' : 'Os lotes são criados a cada compra.'}
            />
          ) : (
            result && (
              <>
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>Produto</th>
                      <th className="d-none d-md-table-cell">Lote</th>
                      <th>Validade</th>
                      <th className="text-end">Saldo</th>
                      <th className="d-none d-md-table-cell text-end">Custo unit.</th>
                      <th className="d-none d-md-table-cell">Origem</th>
                      <th className="text-end">Ações</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((batch) => (
                      <tr key={batch.id}>
                        <td>{batch.productName}</td>
                        <td className="d-none d-md-table-cell">
                          <span className="font-monospace">{batch.batchNumber}</span>
                          {batch.manufacturingDate && (
                            <div>
                              <small className="text-secondary">fabr. {formatDate(batch.manufacturingDate)}</small>
                            </div>
                          )}
                        </td>
                        <td>
                          <ExpirationBadge expirationDate={batch.expirationDate} daysToExpire={batch.daysToExpire} />
                        </td>
                        <td className="text-end">
                          <span className="fw-medium">{batch.quantityAvailable}</span>
                          <small className="text-secondary"> / {batch.quantityReceived}</small>
                        </td>
                        <td className="d-none d-md-table-cell text-end">{formatUnitCost(batch.unitCost)}</td>
                        <td className="d-none d-md-table-cell small">
                          {batch.purchaseId ? (
                            <>
                              Compra #{batch.purchaseId}
                              <div className="text-secondary">{batch.supplierName}</div>
                            </>
                          ) : (
                            <span className="text-secondary">Saldo inicial</span>
                          )}
                        </td>
                        <td className="table-actions">
                          {batch.isExpired && batch.quantityAvailable > 0 && (
                            <ActionButton
                              icon="bi-box-arrow-down"
                              label="Dar baixa"
                              variant="outline-danger"
                              onClick={() => setToWriteOff(batch)}
                            />
                          )}
                        </td>
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

      <ConfirmDialog
        show={toWriteOff !== null}
        title="Baixa de lote vencido"
        message={`Retirar do estoque as ${toWriteOff?.quantityAvailable} unidade(s) do lote ${toWriteOff?.batchNumber} de "${toWriteOff?.productName}"? A saída fica registrada como vencimento.`}
        confirmLabel="Dar baixa"
        busy={writingOff}
        onConfirm={handleWriteOff}
        onCancel={() => setToWriteOff(null)}
      />
    </>
  )
}
