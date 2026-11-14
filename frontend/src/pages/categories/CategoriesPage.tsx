import { useEffect, useState } from 'react'
import { Badge, Button, Card, Col, Form, Row, Table } from 'react-bootstrap'
import { categoriesApi } from '../../api/categories'
import { getErrorMessage } from '../../api/http'
import { access } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { ActionButton } from '../../components/ActionButton'
import { AppAlert, type Feedback } from '../../components/AppAlert'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState } from '../../components/EmptyState'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import { SearchInput } from '../../components/SearchInput'
import { useDebounce } from '../../hooks/useDebounce'
import type { PagedResult } from '../../types/api'
import type { Category } from '../../types/category'
import { CategoryFormModal } from './CategoryFormModal'

type ActiveFilter = 'true' | 'false' | 'all'

export function CategoriesPage() {
  const canEdit = useAuth().hasRole(access.catalogWrite)
  const [search, setSearch] = useState('')
  const [activeFilter, setActiveFilter] = useState<ActiveFilter>('true')
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<PagedResult<Category> | null>(null)
  // true só até a primeira resposta; depois a tabela antiga fica visível enquanto a nova busca carrega.
  const [loading, setLoading] = useState(true)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  // undefined = modal fechado; null = nova categoria; Category = edição.
  const [editing, setEditing] = useState<Category | null | undefined>(undefined)
  const [toDeactivate, setToDeactivate] = useState<Category | null>(null)
  const [deactivating, setDeactivating] = useState(false)

  const debouncedSearch = useDebounce(search)

  // Busca a lista sempre que um filtro, a página ou o reloadKey mudar.
  useEffect(() => {
    let ignore = false // evita mostrar uma resposta antiga que chegou depois de uma nova busca
    categoriesApi
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
      await categoriesApi.deactivate(toDeactivate.id)
      setFeedback({ type: 'success', message: 'Categoria desativada com sucesso.' })
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
        <h1 className="h4 mb-0">Categorias</h1>
        {canEdit && (
          <Button onClick={() => setEditing(null)}>
            <i className="bi bi-plus-lg me-1" aria-hidden="true" />
            Nova categoria
          </Button>
        )}
      </div>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3">
            <Col md={6} lg={5}>
              <SearchInput
                value={search}
                placeholder="Buscar por nome"
                onChange={(value) => {
                  setSearch(value)
                  setPage(1)
                }}
              />
            </Col>
            <Col xs={6} md={3} lg={2}>
              <Form.Select
                aria-label="Situação"
                value={activeFilter}
                onChange={(e) => {
                  setActiveFilter(e.target.value as ActiveFilter)
                  setPage(1)
                }}
              >
                <option value="true">Ativas</option>
                <option value="false">Inativas</option>
                <option value="all">Todas</option>
              </Form.Select>
            </Col>
          </Row>

          {loading && !result ? (
            <Loading />
          ) : result && result.items.length === 0 ? (
            <EmptyState
              title="Nenhuma categoria encontrada"
              description={canEdit ? 'Ajuste a busca ou cadastre uma nova categoria.' : 'Ajuste a busca.'}
            />
          ) : (
            result && (
              <>
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>Nome</th>
                      <th className="d-none d-md-table-cell">Descrição</th>
                      <th className="text-end">Produtos ativos</th>
                      <th>Situação</th>
                      {canEdit && <th className="text-end">Ações</th>}
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((category) => (
                      <tr key={category.id}>
                        <td className="fw-medium">{category.name}</td>
                        <td className="d-none d-md-table-cell text-secondary">{category.description ?? '—'}</td>
                        <td className="text-end">{category.productCount}</td>
                        <td>
                          <Badge bg={category.active ? 'success' : 'secondary'}>
                            {category.active ? 'Ativa' : 'Inativa'}
                          </Badge>
                        </td>
                        {canEdit && (
                          <td className="table-actions">
                            <ActionButton icon="bi-pencil" label="Editar" onClick={() => setEditing(category)} />
                            {category.active && (
                              <ActionButton
                                icon="bi-slash-circle"
                                label="Desativar"
                                variant="outline-danger"
                                onClick={() => setToDeactivate(category)}
                              />
                            )}
                          </td>
                        )}
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
        <CategoryFormModal category={editing} onClose={() => setEditing(undefined)} onSaved={handleSaved} />
      )}

      <ConfirmDialog
        show={toDeactivate !== null}
        title="Desativar categoria"
        message={`Deseja desativar a categoria "${toDeactivate?.name}"? Ela deixa de aparecer nos cadastros, mas o histórico é mantido.`}
        confirmLabel="Desativar"
        busy={deactivating}
        onConfirm={handleDeactivate}
        onCancel={() => setToDeactivate(null)}
      />
    </>
  )
}
