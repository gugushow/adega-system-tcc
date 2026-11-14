import { useEffect, useState } from 'react'
import { Badge, Button, Card, Col, Form, Row, Table } from 'react-bootstrap'
import { combosApi } from '../../api/combos'
import { getErrorMessage } from '../../api/http'
import { ActionButton } from '../../components/ActionButton'
import { AppAlert, type Feedback } from '../../components/AppAlert'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState } from '../../components/EmptyState'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import { SearchInput } from '../../components/SearchInput'
import { useDebounce } from '../../hooks/useDebounce'
import type { PagedResult } from '../../types/api'
import type { Combo } from '../../types/combo'
import { formatCurrency, formatPercent } from '../../utils/format'
import { ComboFormModal } from './ComboFormModal'

type ActiveFilter = 'true' | 'false' | 'all'

/**
 * Combos (só ADMIN): um preço especial para um conjunto de produtos.
 * Vender um combo baixa o estoque de cada produto dele, pelo mesmo caminho da venda avulsa.
 */
export function CombosPage() {
  const [search, setSearch] = useState('')
  const [activeFilter, setActiveFilter] = useState<ActiveFilter>('true')
  const [page, setPage] = useState(1)

  const [result, setResult] = useState<PagedResult<Combo> | null>(null)
  const [loading, setLoading] = useState(true)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [editing, setEditing] = useState<Combo | null | undefined>(undefined)
  const [toDeactivate, setToDeactivate] = useState<Combo | null>(null)
  const [deactivating, setDeactivating] = useState(false)

  const debouncedSearch = useDebounce(search)

  useEffect(() => {
    let ignore = false
    combosApi
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
      await combosApi.deactivate(toDeactivate.id)
      setFeedback({ type: 'success', message: 'Combo desativado com sucesso.' })
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
        <h1 className="h4 mb-0">Combos</h1>
        <Button onClick={() => setEditing(null)}>
          <i className="bi bi-plus-lg me-1" aria-hidden="true" />
          Novo combo
        </Button>
      </div>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3">
            <Col md={6} lg={5}>
              <SearchInput
                value={search}
                placeholder="Buscar combo pelo nome"
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
                <option value="true">Ativos</option>
                <option value="false">Inativos</option>
                <option value="all">Todos</option>
              </Form.Select>
            </Col>
          </Row>

          {loading && !result ? (
            <Loading />
          ) : result && result.items.length === 0 ? (
            <EmptyState title="Nenhum combo encontrado" description="Ajuste os filtros ou cadastre um novo combo." />
          ) : (
            result && (
              <>
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>Combo</th>
                      <th className="d-none d-md-table-cell">Produtos</th>
                      <th className="text-end">Preço</th>
                      <th className="d-none d-md-table-cell text-end">Economia</th>
                      <th className="text-end" title="Quantos combos dá para montar com o estoque atual">
                        Disponíveis
                      </th>
                      <th>Situação</th>
                      <th className="text-end">Ações</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((combo) => (
                      <tr key={combo.id}>
                        <td>
                          <div className="fw-medium">{combo.name}</div>
                          {combo.description && <small className="text-secondary">{combo.description}</small>}
                        </td>
                        <td className="d-none d-md-table-cell small">
                          {combo.items.map((item) => (
                            <div key={item.productId}>
                              {item.quantity}x {item.productName}
                              {!item.productActive && <span className="text-danger"> (inativo)</span>}
                            </div>
                          ))}
                        </td>
                        <td className="text-end text-nowrap">
                          <div className="fw-medium">{formatCurrency(combo.salePrice)}</div>
                          <small className="text-secondary text-decoration-line-through">
                            {formatCurrency(combo.normalPrice)}
                          </small>
                        </td>
                        <td className="d-none d-md-table-cell text-end text-nowrap">
                          {/* Se um produto mudou de preço, o combo pode ter ficado mais caro que os avulsos. */}
                          {combo.savings < 0 ? (
                            <Badge bg="danger" title="O PDV não deixa vender o combo assim. Ajuste o preço.">
                              Mais caro que avulso
                            </Badge>
                          ) : (
                            <>
                              {formatCurrency(combo.savings)}
                              <div className="small text-secondary">{formatPercent(combo.savingsPercent)}</div>
                            </>
                          )}
                        </td>
                        <td className="text-end">
                          {combo.availableQuantity > 0 ? (
                            combo.availableQuantity
                          ) : (
                            <Badge bg="danger">Sem estoque</Badge>
                          )}
                        </td>
                        <td>
                          <Badge bg={combo.active ? 'success' : 'secondary'}>{combo.active ? 'Ativo' : 'Inativo'}</Badge>
                        </td>
                        <td className="table-actions">
                          <ActionButton icon="bi-pencil" label="Editar" onClick={() => setEditing(combo)} />
                          {combo.active && (
                            <ActionButton
                              icon="bi-slash-circle"
                              label="Desativar"
                              variant="outline-danger"
                              onClick={() => setToDeactivate(combo)}
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
        <ComboFormModal combo={editing} onClose={() => setEditing(undefined)} onSaved={handleSaved} />
      )}

      <ConfirmDialog
        show={toDeactivate !== null}
        title="Desativar combo"
        message={`Deseja desativar "${toDeactivate?.name}"? Ele sai do PDV, mas as vendas já feitas continuam no histórico.`}
        confirmLabel="Desativar"
        busy={deactivating}
        onConfirm={handleDeactivate}
        onCancel={() => setToDeactivate(null)}
      />
    </>
  )
}
