import { useEffect, useState } from 'react'
import { Button, Card, Col, Form, Row, Table } from 'react-bootstrap'
import { useLocation, useNavigate } from 'react-router'
import { getErrorMessage } from '../../api/http'
import { purchasesApi } from '../../api/purchases'
import { suppliersApi } from '../../api/suppliers'
import { ActionButton } from '../../components/ActionButton'
import { AppAlert, type Feedback } from '../../components/AppAlert'
import { DateInput } from '../../components/DateInput'
import { EmptyState } from '../../components/EmptyState'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import type { PagedResult } from '../../types/api'
import type { Purchase } from '../../types/purchase'
import type { Supplier } from '../../types/supplier'
import { formatCurrency, formatDate } from '../../utils/format'
import { PurchaseDetailsModal } from './PurchaseDetailsModal'

export function PurchasesPage() {
  const location = useLocation()
  const navigate = useNavigate()

  const [supplierId, setSupplierId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)

  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [result, setResult] = useState<PagedResult<Purchase> | null>(null)
  const [loading, setLoading] = useState(true)
  // A tela de nova compra volta para cá com a mensagem de sucesso em location.state.
  const [feedback, setFeedback] = useState<Feedback | null>(
    location.state?.message ? { type: 'success', message: location.state.message as string } : null,
  )
  const [viewingId, setViewingId] = useState<number | null>(null)

  useEffect(() => {
    // Limpa a mensagem do histórico do navegador, para ela não voltar ao recarregar a página.
    if (location.state?.message) navigate(location.pathname, { replace: true, state: null })
  }, [location, navigate])

  useEffect(() => {
    // Todos os fornecedores (inclusive inativos), para filtrar compras antigas.
    suppliersApi
      .list({ pageSize: 100 })
      .then((data) => setSuppliers(data.items))
      .catch((err) => setFeedback({ type: 'danger', message: getErrorMessage(err) }))
  }, [])

  useEffect(() => {
    let ignore = false
    purchasesApi
      .list({
        supplierId: supplierId ? Number(supplierId) : undefined,
        from: from || undefined,
        to: to || undefined,
        page,
        pageSize: 10,
      })
      .then((data) => !ignore && setResult(data))
      .catch((err) => !ignore && setFeedback({ type: 'danger', message: getErrorMessage(err) }))
      .finally(() => !ignore && setLoading(false))
    return () => {
      ignore = true
    }
  }, [supplierId, from, to, page])

  const changeFilter = (apply: () => void) => {
    apply()
    setPage(1)
  }

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h4 mb-0">Compras</h1>
        <Button onClick={() => navigate('/compras/nova')}>
          <i className="bi bi-plus-lg me-1" aria-hidden="true" />
          Nova compra
        </Button>
      </div>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3">
            <Col md={6} lg={4}>
              <Form.Select
                aria-label="Fornecedor"
                value={supplierId}
                onChange={(e) => changeFilter(() => setSupplierId(e.target.value))}
              >
                <option value="">Todos os fornecedores</option>
                {suppliers.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </Form.Select>
            </Col>
            <Col xs={6} md={3}>
              <DateInput label="De" value={from} max={to} onChange={(value) => changeFilter(() => setFrom(value))} />
            </Col>
            <Col xs={6} md={3}>
              <DateInput label="Até" value={to} min={from} onChange={(value) => changeFilter(() => setTo(value))} />
            </Col>
          </Row>

          {loading && !result ? (
            <Loading />
          ) : result && result.items.length === 0 ? (
            <EmptyState title="Nenhuma compra encontrada" description="Ajuste os filtros ou registre uma nova compra." />
          ) : (
            result && (
              <>
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Data</th>
                      <th>Fornecedor</th>
                      <th className="d-none d-md-table-cell">Nota fiscal</th>
                      <th className="d-none d-md-table-cell text-end">Itens</th>
                      <th className="text-end">Total</th>
                      <th className="text-end">Ações</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((purchase) => (
                      <tr key={purchase.id}>
                        <td className="text-secondary">{purchase.id}</td>
                        <td>{formatDate(purchase.purchaseDate)}</td>
                        <td>{purchase.supplierName}</td>
                        <td className="d-none d-md-table-cell">{purchase.invoiceNumber ?? '—'}</td>
                        <td className="d-none d-md-table-cell text-end">{purchase.itemCount}</td>
                        <td className="text-end fw-medium">{formatCurrency(purchase.totalAmount)}</td>
                        <td className="table-actions">
                          <ActionButton icon="bi-eye" label="Detalhes" onClick={() => setViewingId(purchase.id)} />
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

      {viewingId !== null && <PurchaseDetailsModal purchaseId={viewingId} onClose={() => setViewingId(null)} />}
    </>
  )
}
