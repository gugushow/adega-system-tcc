import { useEffect, useState } from 'react'
import { Alert, Badge, Button, Col, Form, Modal, Row, Table } from 'react-bootstrap'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { salesApi } from '../../api/sales'
import { access } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Loading } from '../../components/Loading'
import { RequiredMark } from '../../components/RequiredMark'
import { paymentMethodLabels, type SaleDetails } from '../../types/sale'
import { formatCurrency, formatDate, formatDateTime } from '../../utils/format'
import { SaleStatusBadge } from './SaleStatusBadge'

interface SaleDetailsModalProps {
  saleId: number
  onClose: () => void
  /** Chamado depois de um cancelamento, para a lista recarregar. */
  onCanceled: (message: string) => void
}

/**
 * Detalhes da venda: itens, de quais lotes saiu cada unidade, custo e lucro.
 * Também é aqui que o ADMIN cancela a venda (com motivo obrigatório).
 */
export function SaleDetailsModal({ saleId, onClose, onCanceled }: SaleDetailsModalProps) {
  // Cancelar venda é só para o ADMIN (o backend responde 403 para os outros perfis).
  const canCancel = useAuth().hasRole(access.admin)
  const [sale, setSale] = useState<SaleDetails | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [canceling, setCanceling] = useState(false)
  const [reason, setReason] = useState('')
  const [reasonError, setReasonError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let ignore = false
    salesApi
      .get(saleId)
      .then((data) => !ignore && setSale(data))
      .catch((err) => !ignore && setError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [saleId])

  async function handleCancel() {
    setBusy(true)
    setError(null)
    setReasonError(null)
    try {
      const canceled = await salesApi.cancel(saleId, reason)
      setSale(canceled)
      setCanceling(false)
      onCanceled(`Venda #${saleId} cancelada. As unidades voltaram para os lotes de origem.`)
    } catch (err) {
      setReasonError(getFieldErrors(err).reason ?? null)
      if (!getFieldErrors(err).reason) setError(getErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal show onHide={onClose} size="lg" centered>
      <Modal.Header closeButton closeLabel="Fechar">
        <Modal.Title as="h5">
          Venda #{saleId} {sale && <SaleStatusBadge status={sale.status} />}
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {error && <Alert variant="danger">{error}</Alert>}
        {!sale && !error && <Loading />}
        {sale && (
          <>
            <Row className="g-2 mb-3 small">
              <Col sm={6}>
                <div><span className="text-secondary">Data:</span> {formatDateTime(sale.saleDate)}</div>
                <div><span className="text-secondary">Cliente:</span> {sale.customerName ?? 'Não informado'}</div>
                {sale.loyaltyPoints > 0 && (
                  <div>
                    <span className="text-secondary">Pontos de fidelidade:</span> {sale.loyaltyPoints}
                    {sale.status === 'CANCELED' && <span className="text-secondary"> (estornados no cancelamento)</span>}
                  </div>
                )}
              </Col>
              <Col sm={6}>
                <div><span className="text-secondary">Vendedor:</span> {sale.userName}</div>
                <div><span className="text-secondary">Pagamento:</span> {paymentMethodLabels[sale.paymentMethod]}</div>
              </Col>
            </Row>

            {sale.status === 'CANCELED' && (
              <Alert variant="secondary" className="py-2 small">
                Cancelada em {sale.canceledAt && formatDateTime(sale.canceledAt)} por {sale.canceledByUserName}.
                <br />
                Motivo: {sale.cancelReason}
              </Alert>
            )}

            <Table size="sm" responsive>
              <thead>
                <tr>
                  <th>Produto</th>
                  <th>Lotes usados (FEFO)</th>
                  <th className="text-end">Qtd.</th>
                  <th className="text-end">Preço</th>
                  <th className="text-end">Desconto</th>
                  <th className="text-end">Total</th>
                </tr>
              </thead>
              <tbody>
                {sale.items.map((item) => (
                  <tr key={item.id}>
                    <td>
                      {item.productName}
                      {/* Item de combo: o desconto da linha é a parte dele na economia do combo. */}
                      {item.comboName && (
                        <div>
                          <Badge bg="info">
                            Combo: {item.comboName}
                          </Badge>
                        </div>
                      )}
                      <div className="small text-secondary">custo médio {formatCurrency(item.costPrice)}</div>
                    </td>
                    <td className="small">
                      {item.batches.map((b) => (
                        <div key={b.batchId}>
                          <span className="font-monospace">{b.batchNumber}</span>: {b.quantity} un.
                          <span className="text-secondary">
                            {' '}
                            · {b.expirationDate ? `val. ${formatDate(b.expirationDate)}` : 'sem validade'}
                          </span>
                        </div>
                      ))}
                    </td>
                    <td className="text-end">{item.quantity}</td>
                    <td className="text-end">{formatCurrency(item.unitPrice)}</td>
                    <td className="text-end">{item.discount > 0 ? formatCurrency(item.discount) : '—'}</td>
                    <td className="text-end">{formatCurrency(item.total)}</td>
                  </tr>
                ))}
              </tbody>
            </Table>

            <Row className="justify-content-end">
              <Col sm={7} md={6}>
                <Table size="sm" borderless className="mb-0">
                  <tbody>
                    <tr>
                      <td>Subtotal</td>
                      <td className="text-end">{formatCurrency(sale.subtotal)}</td>
                    </tr>
                    <tr>
                      <td>Desconto</td>
                      <td className="text-end">− {formatCurrency(sale.discount)}</td>
                    </tr>
                    <tr className="fw-semibold border-top">
                      <td>Total</td>
                      <td className="text-end">{formatCurrency(sale.total)}</td>
                    </tr>
                    <tr className="text-secondary">
                      <td>Custo das mercadorias</td>
                      <td className="text-end">{formatCurrency(sale.totalCost)}</td>
                    </tr>
                    <tr className={sale.profit >= 0 ? 'text-success' : 'text-danger'}>
                      <td>Lucro</td>
                      <td className="text-end">{formatCurrency(sale.profit)}</td>
                    </tr>
                  </tbody>
                </Table>
              </Col>
            </Row>

            {canceling && (
              <Form.Group controlId="cancel-reason" className="mt-3">
                <Form.Label>Motivo do cancelamento<RequiredMark /></Form.Label>
                <Form.Control
                  as="textarea"
                  rows={2}
                  maxLength={255}
                  value={reason}
                  autoFocus
                  isInvalid={!!reasonError}
                  onChange={(e) => setReason(e.target.value)}
                />
                <Form.Control.Feedback type="invalid">{reasonError}</Form.Control.Feedback>
                <Form.Text>
                  As unidades voltam para os mesmos lotes de onde saíram, e a venda deixa de contar no faturamento.
                  {sale.loyaltyPoints > 0 && ` Os ${sale.loyaltyPoints} ponto(s) que a venda deu ao cliente são estornados.`}
                </Form.Text>
              </Form.Group>
            )}
          </>
        )}
      </Modal.Body>
      <Modal.Footer>
        {canCancel &&
          sale?.status === 'COMPLETED' &&
          (canceling ? (
            <>
              <Button variant="outline-secondary" className="me-auto" onClick={() => setCanceling(false)} disabled={busy}>
                Voltar
              </Button>
              <Button variant="danger" onClick={handleCancel} disabled={busy}>
                {busy ? 'Cancelando...' : 'Confirmar cancelamento'}
              </Button>
            </>
          ) : (
            <Button variant="outline-danger" className="me-auto" onClick={() => setCanceling(true)}>
              Cancelar venda
            </Button>
          ))}
        {!canceling && (
          <Button variant="outline-secondary" onClick={onClose}>
            Fechar
          </Button>
        )}
      </Modal.Footer>
    </Modal>
  )
}
