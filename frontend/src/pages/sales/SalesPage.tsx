import { useEffect, useState } from 'react'
import { Button, Card, Col, Form, Row, Table } from 'react-bootstrap'
import { useNavigate } from 'react-router'
import { getErrorMessage } from '../../api/http'
import { salesApi } from '../../api/sales'
import { ActionButton } from '../../components/ActionButton'
import { AppAlert, type Feedback } from '../../components/AppAlert'
import { DateInput } from '../../components/DateInput'
import { EmptyState } from '../../components/EmptyState'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import type { PagedResult } from '../../types/api'
import { paymentMethodLabels, type PaymentMethod, type Sale, type SaleStatus } from '../../types/sale'
import { formatCurrency, formatDateTime } from '../../utils/format'
import { SaleDetailsModal } from './SaleDetailsModal'
import { SaleStatusBadge } from './SaleStatusBadge'

/** Histórico de vendas com filtros por período, situação e forma de pagamento. */
export function SalesPage() {
  const navigate = useNavigate()

  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [status, setStatus] = useState<SaleStatus | ''>('')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod | ''>('')
  const [page, setPage] = useState(1)

  const [result, setResult] = useState<PagedResult<Sale> | null>(null)
  const [loading, setLoading] = useState(true)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [reloadKey, setReloadKey] = useState(0)
  const [viewingId, setViewingId] = useState<number | null>(null)

  useEffect(() => {
    let ignore = false
    salesApi
      .list({
        from: from || undefined,
        to: to || undefined,
        status: status || undefined,
        paymentMethod: paymentMethod || undefined,
        page,
        pageSize: 10,
      })
      .then((data) => !ignore && setResult(data))
      .catch((err) => !ignore && setFeedback({ type: 'danger', message: getErrorMessage(err) }))
      .finally(() => !ignore && setLoading(false))
    return () => {
      ignore = true
    }
  }, [from, to, status, paymentMethod, page, reloadKey])

  const changeFilter = (apply: () => void) => {
    apply()
    setPage(1)
  }

  function handleCanceled(message: string) {
    setFeedback({ type: 'success', message })
    setReloadKey((key) => key + 1)
  }

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h4 mb-0">Histórico de vendas</h1>
        <Button onClick={() => navigate('/vendas')}>Abrir PDV</Button>
      </div>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3">
            <Col xs={6} md={3}>
              <DateInput label="De" value={from} max={to} onChange={(value) => changeFilter(() => setFrom(value))} />
            </Col>
            <Col xs={6} md={3}>
              <DateInput label="Até" value={to} min={from} onChange={(value) => changeFilter(() => setTo(value))} />
            </Col>
            <Col xs={6} md={3}>
              <Form.Select
                aria-label="Situação"
                value={status}
                onChange={(e) => changeFilter(() => setStatus(e.target.value as SaleStatus | ''))}
              >
                <option value="">Todas as situações</option>
                <option value="COMPLETED">Concluídas</option>
                <option value="CANCELED">Canceladas</option>
              </Form.Select>
            </Col>
            <Col xs={6} md={3}>
              <Form.Select
                aria-label="Forma de pagamento"
                value={paymentMethod}
                onChange={(e) => changeFilter(() => setPaymentMethod(e.target.value as PaymentMethod | ''))}
              >
                <option value="">Todos os pagamentos</option>
                {Object.entries(paymentMethodLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </Form.Select>
            </Col>
          </Row>

          {loading && !result ? (
            <Loading />
          ) : result && result.items.length === 0 ? (
            <EmptyState title="Nenhuma venda encontrada" description="Ajuste os filtros ou faça uma venda pelo PDV." />
          ) : (
            result && (
              <>
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Data</th>
                      <th>Cliente</th>
                      <th className="d-none d-md-table-cell">Pagamento</th>
                      <th className="d-none d-md-table-cell text-end">Itens</th>
                      <th className="text-end">Total</th>
                      <th>Situação</th>
                      <th className="text-end">Ações</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((sale) => (
                      <tr key={sale.id} className={sale.status === 'CANCELED' ? 'text-secondary' : undefined}>
                        <td className="text-secondary">{sale.id}</td>
                        <td className="text-nowrap">{formatDateTime(sale.saleDate)}</td>
                        <td>{sale.customerName ?? '—'}</td>
                        <td className="d-none d-md-table-cell">{paymentMethodLabels[sale.paymentMethod]}</td>
                        <td className="d-none d-md-table-cell text-end">{sale.itemCount}</td>
                        <td className="text-end text-nowrap">
                          {sale.status === 'CANCELED' ? <s>{formatCurrency(sale.total)}</s> : formatCurrency(sale.total)}
                        </td>
                        <td>
                          <SaleStatusBadge status={sale.status} />
                        </td>
                        <td className="table-actions">
                          <ActionButton icon="bi-eye" label="Detalhes" onClick={() => setViewingId(sale.id)} />
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

      {viewingId !== null && (
        <SaleDetailsModal saleId={viewingId} onClose={() => setViewingId(null)} onCanceled={handleCanceled} />
      )}
    </>
  )
}
