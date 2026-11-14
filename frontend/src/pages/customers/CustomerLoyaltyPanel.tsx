import { useEffect, useState, type FormEvent } from 'react'
import { Alert, Badge, Button, Col, Form, Row, Table } from 'react-bootstrap'
import { customersApi } from '../../api/customers'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { access } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import { FieldLabel, RequiredMark } from '../../components/RequiredMark'
import type { Customer } from '../../types/customer'
import { loyaltyTypeLabels, type LoyaltyStatement, type LoyaltyType } from '../../types/loyalty'
import { formatCurrency, formatDateTime } from '../../utils/format'

const typeVariant: Record<LoyaltyType, string> = {
  EARN: 'success',
  REDEEM: 'primary',
  ADJUSTMENT: 'secondary',
}

type Operation = 'redeem' | 'adjust' | null

/**
 * Pontos de fidelidade do cliente: saldo, extrato e as operações de resgate (ADMIN e vendedor)
 * e ajuste manual (só ADMIN). O saldo é sempre a soma do extrato, calculada pela API.
 */
export function CustomerLoyaltyPanel({ customer }: { customer: Customer }) {
  const canAdjust = useAuth().hasRole(access.admin)
  const [statement, setStatement] = useState<LoyaltyStatement | null>(null)
  const [page, setPage] = useState(1)
  const [reloadKey, setReloadKey] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const [operation, setOperation] = useState<Operation>(null)
  const [points, setPoints] = useState('')
  const [text, setText] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let ignore = false
    customersApi
      .loyalty(customer.id, { page, pageSize: 8 })
      .then((data) => !ignore && setStatement(data))
      .catch((err) => !ignore && setError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [customer.id, page, reloadKey])

  function open(next: Operation) {
    setOperation(next)
    setPoints('')
    setText('')
    setFieldErrors({})
    setError(null)
    setSuccess(null)
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})
    const value = Number(points) || 0
    try {
      if (operation === 'redeem') {
        await customersApi.redeemPoints(customer.id, { points: value, description: text })
        setSuccess(`Resgate de ${value} ponto(s) registrado.`)
      } else {
        await customersApi.adjustPoints(customer.id, { points: value, reason: text })
        setSuccess('Ajuste de pontos registrado.')
      }
      setOperation(null)
      setPage(1)
      setReloadKey((key) => key + 1)
    } catch (err) {
      const fields = getFieldErrors(err)
      // O motivo do ajuste se chama "reason" na API; na tela é o mesmo campo de texto da descrição.
      if (fields.reason) fields.description = fields.reason
      setFieldErrors(fields)
      if (Object.keys(fields).length === 0) setError(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  if (!statement) {
    return error ? <Alert variant="danger">{error}</Alert> : <Loading />
  }

  const transactions = statement.transactions

  return (
    <>
      {success && (
        <Alert variant="success" className="py-2" dismissible closeLabel="Fechar" onClose={() => setSuccess(null)}>
          {success}
        </Alert>
      )}
      {error && <Alert variant="danger" className="py-2">{error}</Alert>}

      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <div>
          <div className="text-secondary small">Saldo de pontos</div>
          <div className={`fs-3 fw-semibold ${statement.balance < 0 ? 'text-danger' : ''}`} data-testid="loyalty-balance">
            {statement.balance}
          </div>
          <div className="small text-secondary">
            {statement.programEnabled
              ? `Regra atual: ${formatCurrency(statement.amountPerPoint)} em compras = 1 ponto.`
              : 'O programa está desligado: novas vendas não dão pontos.'}
          </div>
        </div>
        {customer.active && operation === null && (
          <div className="d-flex gap-2">
            <Button size="sm" onClick={() => open('redeem')} disabled={statement.balance <= 0}>
              Resgatar pontos
            </Button>
            {canAdjust && (
              <Button size="sm" variant="outline-secondary" onClick={() => open('adjust')}>
                Ajustar
              </Button>
            )}
          </div>
        )}
      </div>

      {statement.balance < 0 && (
        <Alert variant="warning" className="py-2 small">
          Saldo negativo: o cliente resgatou pontos de uma venda que depois foi cancelada. Novas compras vão cobrir a
          diferença.
        </Alert>
      )}

      {operation && (
        <Form onSubmit={handleSubmit} noValidate className="border rounded p-3 mb-3 bg-body-tertiary">
          <h6>{operation === 'redeem' ? 'Resgatar pontos' : 'Ajuste manual de pontos'}</h6>
          <Row className="g-2">
            <Col sm={4}>
              <Form.Group controlId="loyalty-points">
                <Form.Label className="small">Pontos<RequiredMark /></Form.Label>
                <Form.Control
                  type="number"
                  min={operation === 'redeem' ? 1 : undefined}
                  value={points}
                  autoFocus
                  onChange={(e) => setPoints(e.target.value)}
                  isInvalid={!!fieldErrors.points}
                />
                <Form.Control.Feedback type="invalid">{fieldErrors.points}</Form.Control.Feedback>
                {operation === 'adjust' && <Form.Text>Positivo soma, negativo tira.</Form.Text>}
              </Form.Group>
            </Col>
            <Col sm={8}>
              <Form.Group controlId="loyalty-text">
                <Form.Label className="small">
                  <FieldLabel text={operation === 'redeem' ? 'Trocado por *' : 'Motivo *'} />
                </Form.Label>
                <Form.Control
                  value={text}
                  maxLength={255}
                  placeholder={operation === 'redeem' ? 'Ex.: 1 Heineken grátis' : 'Ex.: pontos de uma compra antiga'}
                  onChange={(e) => setText(e.target.value)}
                  isInvalid={!!fieldErrors.description}
                />
                <Form.Control.Feedback type="invalid">{fieldErrors.description}</Form.Control.Feedback>
              </Form.Group>
            </Col>
          </Row>
          <div className="d-flex justify-content-end gap-2 mt-3">
            <Button size="sm" variant="outline-secondary" onClick={() => setOperation(null)} disabled={saving}>
              Cancelar
            </Button>
            <Button size="sm" type="submit" disabled={saving}>
              {saving ? 'Salvando...' : operation === 'redeem' ? 'Confirmar resgate' : 'Confirmar ajuste'}
            </Button>
          </div>
        </Form>
      )}

      <h6>Extrato</h6>
      {transactions.items.length === 0 ? (
        <p className="text-secondary mb-0">Nenhuma movimentação de pontos ainda.</p>
      ) : (
        <>
          <Table size="sm" responsive className="align-middle">
            <thead>
              <tr>
                <th>Data</th>
                <th>Tipo</th>
                <th>Descrição</th>
                <th className="text-end">Pontos</th>
              </tr>
            </thead>
            <tbody>
              {transactions.items.map((t) => (
                <tr key={t.id}>
                  <td className="text-nowrap small">{formatDateTime(t.createdAt)}</td>
                  <td>
                    <Badge bg={typeVariant[t.type]}>{loyaltyTypeLabels[t.type]}</Badge>
                  </td>
                  <td className="small">
                    {t.description}
                    <div className="text-secondary">por {t.userName}</div>
                  </td>
                  <td className={`text-end fw-semibold ${t.points < 0 ? 'text-danger' : 'text-success'}`}>
                    {t.points > 0 ? `+${t.points}` : t.points}
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
          <Pagination
            page={transactions.page}
            totalPages={transactions.totalPages}
            totalItems={transactions.totalItems}
            onPageChange={setPage}
          />
        </>
      )}
    </>
  )
}
