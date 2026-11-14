import { useEffect, useState } from 'react'
import { Badge, Button, Card, Col, Form, Row, Table } from 'react-bootstrap'
import { getErrorMessage } from '../../api/http'
import { suppliersApi } from '../../api/suppliers'
import { ActionButton } from '../../components/ActionButton'
import { AppAlert, type Feedback } from '../../components/AppAlert'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState } from '../../components/EmptyState'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import { SearchInput } from '../../components/SearchInput'
import { useDebounce } from '../../hooks/useDebounce'
import type { PagedResult } from '../../types/api'
import type { Supplier } from '../../types/supplier'
import { formatCnpj } from '../../utils/format'
import { SupplierFormModal } from './SupplierFormModal'
import { SupplierSummaryModal } from './SupplierSummaryModal'

type ActiveFilter = 'true' | 'false' | 'all'

export function SuppliersPage() {
  const [search, setSearch] = useState('')
  const [activeFilter, setActiveFilter] = useState<ActiveFilter>('true')
  const [page, setPage] = useState(1)

  const [result, setResult] = useState<PagedResult<Supplier> | null>(null)
  const [loading, setLoading] = useState(true)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [editing, setEditing] = useState<Supplier | null | undefined>(undefined)
  const [viewing, setViewing] = useState<Supplier | null>(null)
  const [toDeactivate, setToDeactivate] = useState<Supplier | null>(null)
  const [deactivating, setDeactivating] = useState(false)

  const debouncedSearch = useDebounce(search)

  useEffect(() => {
    let ignore = false
    suppliersApi
      .list({
        search: debouncedSearch || undefined,
        active: activeFilter === 'all' ? undefined : activeFilter === 'true',
        page,
        pageSize: 10,
      })
      .then((data) => !ignore && setResult(data))
      .catch((err) => !ignore && setFeedback({ type: 'danger', message: getErrorMessage(err) }))
      .finally(() => !ignore && setLoading(false))
    return () => {
      ignore = true
    }
  }, [debouncedSearch, activeFilter, page, reloadKey])

  const reload = () => setReloadKey((key) => key + 1)

  function handleSaved(message: string) {
    setEditing(undefined)
    setFeedback({ type: 'success', message })
    reload()
  }

  async function handleDeactivate() {
    if (!toDeactivate) return
    setDeactivating(true)
    try {
      await suppliersApi.deactivate(toDeactivate.id)
      setFeedback({ type: 'success', message: 'Fornecedor desativado com sucesso.' })
      reload()
    } catch (err) {
      setFeedback({ type: 'danger', message: getErrorMessage(err) })
    } finally {
      setDeactivating(false)
      setToDeactivate(null)
    }
  }

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h4 mb-0">Fornecedores</h1>
        <Button onClick={() => setEditing(null)}>
          <i className="bi bi-plus-lg me-1" aria-hidden="true" />
          Novo fornecedor
        </Button>
      </div>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3">
            <Col md={8} lg={6}>
              <SearchInput
                value={search}
                placeholder="Buscar por nome, razão social ou CNPJ"
                onChange={(value) => {
                  setSearch(value)
                  setPage(1)
                }}
              />
            </Col>
            <Col md={4} lg={2}>
              <Form.Select
                aria-label="Situação"
                value={activeFilter}
                onChange={(e) => {
                  setActiveFilter(e.target.value as ActiveFilter)
                  setPage(1)
                }}
              >
                <option value="true">Ativos</option>
                <option value="false">Inativos</option>
                <option value="all">Todos</option>
              </Form.Select>
            </Col>
          </Row>

          {loading && !result ? (
            <Loading />
          ) : result && result.items.length === 0 ? (
            <EmptyState title="Nenhum fornecedor encontrado" description="Ajuste os filtros ou cadastre um novo fornecedor." />
          ) : (
            result && (
              <>
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>Fornecedor</th>
                      <th className="d-none d-md-table-cell">CNPJ</th>
                      <th className="d-none d-md-table-cell">Contato</th>
                      <th>Situação</th>
                      <th className="text-end">Ações</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((supplier) => (
                      <tr key={supplier.id}>
                        <td>
                          <div className="fw-medium">{supplier.name}</div>
                          <small className="text-secondary">{supplier.corporateName ?? '—'}</small>
                        </td>
                        <td className="d-none d-md-table-cell font-monospace small text-nowrap">
                          {supplier.cnpj ? formatCnpj(supplier.cnpj) : '—'}
                        </td>
                        <td className="d-none d-md-table-cell">
                          <div>{supplier.phone ?? '—'}</div>
                          <small className="text-secondary">{supplier.email}</small>
                        </td>
                        <td>
                          <Badge bg={supplier.active ? 'success' : 'secondary'}>
                            {supplier.active ? 'Ativo' : 'Inativo'}
                          </Badge>
                        </td>
                        <td className="table-actions">
                          <ActionButton icon="bi-eye" label="Resumo" onClick={() => setViewing(supplier)} />
                          <ActionButton icon="bi-pencil" label="Editar" onClick={() => setEditing(supplier)} />
                          {supplier.active && (
                            <ActionButton
                              icon="bi-slash-circle"
                              label="Desativar"
                              variant="outline-danger"
                              onClick={() => setToDeactivate(supplier)}
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

      {editing !== undefined && (
        <SupplierFormModal supplier={editing} onClose={() => setEditing(undefined)} onSaved={handleSaved} />
      )}

      {viewing && <SupplierSummaryModal supplier={viewing} onClose={() => setViewing(null)} />}

      <ConfirmDialog
        show={toDeactivate !== null}
        title="Desativar fornecedor"
        message={`Deseja desativar "${toDeactivate?.name}"? Ele não aparece mais nas novas compras, mas o histórico é mantido.`}
        confirmLabel="Desativar"
        busy={deactivating}
        onConfirm={handleDeactivate}
        onCancel={() => setToDeactivate(null)}
      />
    </>
  )
}
