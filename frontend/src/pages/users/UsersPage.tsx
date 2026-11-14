import { useEffect, useState } from 'react'
import { Badge, Button, Card, Col, Form, Row, Table } from 'react-bootstrap'
import { getErrorMessage } from '../../api/http'
import { usersApi } from '../../api/users'
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
import { roleLabels, type User, type UserRole } from '../../types/user'
import { formatDateTime } from '../../utils/format'
import { UserFormModal } from './UserFormModal'

type ActiveFilter = 'true' | 'false' | 'all'

const roleVariant: Record<UserRole, string> = {
  ADMIN: 'dark',
  VENDEDOR: 'primary',
  ESTOQUISTA: 'info',
}

/** Gestão de usuários (só ADMIN): cadastrar, mudar perfil, trocar senha e desativar. */
export function UsersPage() {
  const { user: currentUser } = useAuth()
  const [search, setSearch] = useState('')
  const [role, setRole] = useState<UserRole | ''>('')
  const [activeFilter, setActiveFilter] = useState<ActiveFilter>('true')
  const [page, setPage] = useState(1)

  const [result, setResult] = useState<PagedResult<User> | null>(null)
  const [loading, setLoading] = useState(true)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [editing, setEditing] = useState<User | null | undefined>(undefined)
  const [toDeactivate, setToDeactivate] = useState<User | null>(null)
  const [deactivating, setDeactivating] = useState(false)

  const debouncedSearch = useDebounce(search)

  useEffect(() => {
    let ignore = false
    usersApi
      .list({
        search: debouncedSearch || undefined,
        role: role || undefined,
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
  }, [debouncedSearch, role, activeFilter, page, reloadKey])

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
      await usersApi.deactivate(toDeactivate.id)
      setFeedback({ type: 'success', message: 'Usuário desativado com sucesso.' })
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
        <h1 className="h4 mb-0">Usuários</h1>
        <Button onClick={() => setEditing(null)}>
          <i className="bi bi-plus-lg me-1" aria-hidden="true" />
          Novo usuário
        </Button>
      </div>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3">
            <Col md={6} lg={5}>
              <SearchInput
                value={search}
                placeholder="Buscar por nome ou e-mail"
                onChange={(value) => {
                  setSearch(value)
                  setPage(1)
                }}
              />
            </Col>
            <Col xs={6} md={3} lg={2}>
              <Form.Select
                aria-label="Perfil"
                value={role}
                onChange={(e) => {
                  setRole(e.target.value as UserRole | '')
                  setPage(1)
                }}
              >
                <option value="">Todos os perfis</option>
                {(Object.keys(roleLabels) as UserRole[]).map((r) => (
                  <option key={r} value={r}>
                    {roleLabels[r]}
                  </option>
                ))}
              </Form.Select>
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
            <EmptyState title="Nenhum usuário encontrado" description="Ajuste os filtros ou cadastre um novo usuário." />
          ) : (
            result && (
              <>
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>Usuário</th>
                      <th>Perfil</th>
                      <th>Situação</th>
                      <th className="d-none d-md-table-cell">Criado em</th>
                      <th className="text-end">Ações</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((user) => (
                      <tr key={user.id}>
                        <td>
                          <div className="fw-medium">
                            {user.name}
                            {user.id === currentUser?.id && <small className="text-muted"> (você)</small>}
                          </div>
                          <small className="text-secondary">{user.email}</small>
                        </td>
                        <td>
                          <Badge bg={roleVariant[user.role]}>{roleLabels[user.role]}</Badge>
                        </td>
                        <td>
                          <Badge bg={user.active ? 'success' : 'secondary'}>{user.active ? 'Ativo' : 'Inativo'}</Badge>
                        </td>
                        <td className="d-none d-md-table-cell text-nowrap small">{formatDateTime(user.createdAt)}</td>
                        <td className="table-actions">
                          <ActionButton icon="bi-pencil" label="Editar" onClick={() => setEditing(user)} />
                          {/* Ninguém desativa a si mesmo (o backend também bloqueia). */}
                          {user.active && user.id !== currentUser?.id && (
                            <ActionButton
                              icon="bi-slash-circle"
                              label="Desativar"
                              variant="outline-danger"
                              onClick={() => setToDeactivate(user)}
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
        <UserFormModal user={editing} onClose={() => setEditing(undefined)} onSaved={handleSaved} />
      )}

      <ConfirmDialog
        show={toDeactivate !== null}
        title="Desativar usuário"
        message={`Deseja desativar "${toDeactivate?.name}"? Ele perde o acesso na hora, mas o histórico de vendas e compras é mantido.`}
        confirmLabel="Desativar"
        busy={deactivating}
        onConfirm={handleDeactivate}
        onCancel={() => setToDeactivate(null)}
      />
    </>
  )
}
