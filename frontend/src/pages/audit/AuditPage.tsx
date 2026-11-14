import { useEffect, useState } from 'react'
import { Badge, Card, Col, Form, ListGroup, Row, Table } from 'react-bootstrap'
import { auditApi } from '../../api/audit'
import { getErrorMessage } from '../../api/http'
import { usersApi } from '../../api/users'
import { AppAlert, type Feedback } from '../../components/AppAlert'
import { DateInput } from '../../components/DateInput'
import { EmptyState } from '../../components/EmptyState'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import type { PagedResult } from '../../types/api'
import { auditActionLabels, auditEntityLabels, type AuditAction, type AuditLog } from '../../types/audit'
import type { User } from '../../types/user'
import { formatDateTime } from '../../utils/format'
import { describeChanges } from './auditChanges'

/** Cor da etiqueta da ação: vermelho para o que tira algo (cancelar, desativar, baixar). */
function actionVariant(action: AuditAction) {
  if (action === 'SALE_CANCELED' || action.endsWith('DEACTIVATED') || action === 'BATCH_WRITTEN_OFF') return 'danger'
  if (action.endsWith('CREATED') || action === 'PRODUCT_REACTIVATED') return 'success'
  return 'secondary'
}

/** "Produto #3". Não repete quando a descrição já diz isso (ex.: "Venda #12"). */
function recordLabel(log: AuditLog) {
  const label = `${auditEntityLabels[log.entity] ?? log.entity}${log.entityId > 0 ? ` #${log.entityId}` : ''}`
  return label === log.description ? null : label
}

/** O que mudou, campo a campo: "Preço de venda: R$ 6,00 → R$ 6,50". */
function Changes({ log }: { log: AuditLog }) {
  const changes = describeChanges(log.oldValue, log.newValue)
  if (changes.length === 0) return <span className="text-secondary">—</span>

  return (
    <ul className="list-unstyled mb-0 small">
      {changes.map((change) => (
        <li key={change.field}>
          <span className="text-secondary">{change.label}:</span>{' '}
          {change.before !== null && change.after !== null ? (
            <>
              <span className="text-decoration-line-through text-secondary">{change.before}</span> → {change.after}
            </>
          ) : (
            (change.after ?? change.before)
          )}
        </li>
      ))}
    </ul>
  )
}

/**
 * Auditoria (só ADMIN): quem fez o quê e quando. A tela só lê: nenhum registro pode ser editado ou
 * apagado, nem pela API. Cada ação importante grava o registro na mesma transação da própria mudança.
 */
export function AuditPage() {
  const [action, setAction] = useState<AuditAction | ''>('')
  const [entity, setEntity] = useState('')
  const [userId, setUserId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)

  const [users, setUsers] = useState<User[]>([])
  const [result, setResult] = useState<PagedResult<AuditLog> | null>(null)
  const [loading, setLoading] = useState(true)
  const [feedback, setFeedback] = useState<Feedback | null>(null)

  useEffect(() => {
    usersApi
      .list({ pageSize: 100 })
      .then((data) => setUsers(data.items))
      .catch(() => setUsers([]))
  }, [])

  useEffect(() => {
    let ignore = false
    auditApi
      .list({
        action: action || undefined,
        entity: entity || undefined,
        userId: userId ? Number(userId) : undefined,
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
  }, [action, entity, userId, from, to, page])

  /** Todo filtro novo volta para a primeira página. */
  const filter =
    <T,>(setter: (value: T) => void) =>
    (value: T) => {
      setter(value)
      setPage(1)
    }

  return (
    <>
      <h1 className="h4 mb-3">Auditoria</h1>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3">
            <Col sm={4} lg={2}>
              <Form.Select aria-label="Ação" value={action} onChange={(e) => filter(setAction)(e.target.value as AuditAction | '')}>
                <option value="">Todas as ações</option>
                {(Object.keys(auditActionLabels) as AuditAction[]).map((a) => (
                  <option key={a} value={a}>
                    {auditActionLabels[a]}
                  </option>
                ))}
              </Form.Select>
            </Col>
            <Col sm={4} lg={2}>
              <Form.Select aria-label="Tipo de registro" value={entity} onChange={(e) => filter(setEntity)(e.target.value)}>
                <option value="">Todos os tipos</option>
                {Object.entries(auditEntityLabels).map(([key, label]) => (
                  <option key={key} value={key}>
                    {label}
                  </option>
                ))}
              </Form.Select>
            </Col>
            <Col sm={4} lg={2}>
              <Form.Select aria-label="Usuário" value={userId} onChange={(e) => filter(setUserId)(e.target.value)}>
                <option value="">Todos os usuários</option>
                {users.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </Form.Select>
            </Col>
            <Col xs={6} lg={3}>
              <DateInput label="De" value={from} max={to} onChange={filter(setFrom)} />
            </Col>
            <Col xs={6} lg={3}>
              <DateInput label="Até" value={to} min={from} onChange={filter(setTo)} />
            </Col>
          </Row>

          {loading && !result ? (
            <Loading />
          ) : result && result.items.length === 0 ? (
            <EmptyState title="Nenhum registro encontrado" description="Ajuste os filtros para ver outras ações." />
          ) : (
            result && (
              <>
                {/* No celular a tabela ficaria larga demais: cada registro vira um cartão. */}
                <ListGroup variant="flush" className="d-md-none mb-3">
                  {result.items.map((log) => (
                    <ListGroup.Item key={log.id} className="px-0">
                      <div className="d-flex justify-content-between align-items-start gap-2 mb-1">
                        <Badge bg={actionVariant(log.action)}>{auditActionLabels[log.action] ?? log.action}</Badge>
                        <small className="text-secondary text-end">
                          {formatDateTime(log.createdAt)}
                          <br />
                          {log.userName}
                        </small>
                      </div>
                      <div className="fw-medium">
                        {log.description}
                        {recordLabel(log) && <small className="text-secondary fw-normal"> · {recordLabel(log)}</small>}
                      </div>
                      <Changes log={log} />
                    </ListGroup.Item>
                  ))}
                </ListGroup>

                <div className="d-none d-md-block">
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>Quando</th>
                      <th>Quem</th>
                      <th>Ação</th>
                      <th>Registro</th>
                      <th>O que mudou</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((log) => (
                      <tr key={log.id}>
                        <td className="text-nowrap small">{formatDateTime(log.createdAt)}</td>
                        <td className="small">{log.userName}</td>
                        <td>
                          <Badge bg={actionVariant(log.action)}>{auditActionLabels[log.action] ?? log.action}</Badge>
                        </td>
                        <td>
                          <div className="fw-medium">{log.description}</div>
                          {recordLabel(log) && <small className="text-secondary">{recordLabel(log)}</small>}
                        </td>
                        <td>
                          <Changes log={log} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
                </div>
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
    </>
  )
}
