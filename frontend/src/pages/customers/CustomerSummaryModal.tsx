import { useEffect, useState } from 'react'
import { Alert, Button, Col, Modal, Row, Tab, Table, Tabs } from 'react-bootstrap'
import { customersApi } from '../../api/customers'
import { getErrorMessage } from '../../api/http'
import { Loading } from '../../components/Loading'
import type { Customer, CustomerSummary } from '../../types/customer'
import { paymentMethodLabels, type Sale } from '../../types/sale'
import { formatCurrency, formatDateTime, formatNumber } from '../../utils/format'
import { SaleStatusBadge } from '../sales/SaleStatusBadge'
import { CustomerLoyaltyPanel } from './CustomerLoyaltyPanel'

interface CustomerSummaryModalProps {
  customer: Customer
  onClose: () => void
}

/**
 * Histórico do cliente em duas abas: compras (número de compras, total gasto, ticket médio, últimas vendas)
 * e pontos de fidelidade (saldo, extrato, resgate e ajuste).
 */
export function CustomerSummaryModal({ customer, onClose }: CustomerSummaryModalProps) {
  const [summary, setSummary] = useState<CustomerSummary | null>(null)
  const [sales, setSales] = useState<Sale[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let ignore = false
    Promise.all([customersApi.summary(customer.id), customersApi.sales(customer.id, { pageSize: 5 })])
      .then(([summaryData, salesData]) => {
        if (ignore) return
        setSummary(summaryData)
        setSales(salesData.items)
      })
      .catch((err) => !ignore && setError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [customer.id])

  const stat = (label: string, value: string) => (
    <Col xs={6} md={3}>
      <div className="text-secondary small">{label}</div>
      <div className="fs-5 fw-semibold">{value}</div>
    </Col>
  )

  return (
    <Modal show onHide={onClose} size="lg" centered>
      <Modal.Header closeButton closeLabel="Fechar">
        <Modal.Title as="h5">{customer.name}</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <Tabs defaultActiveKey="purchases" className="mb-3" mountOnEnter>
          <Tab eventKey="purchases" title="Compras">
        {error && <Alert variant="danger">{error}</Alert>}
        {!summary && !error && <Loading />}
        {summary && (
          <>
            <Row className="g-3 mb-4 text-center">
              {stat('Compras', formatNumber(summary.purchaseCount))}
              {stat('Total gasto', formatCurrency(summary.totalSpent))}
              {stat('Ticket médio', formatCurrency(summary.averageTicket))}
              {stat('Última compra', summary.lastPurchaseDate ? formatDateTime(summary.lastPurchaseDate) : '—')}
            </Row>
            <p className="small text-secondary">Os números consideram só as vendas concluídas (as canceladas ficam de fora).</p>

            <h6>Últimas vendas</h6>
            {sales.length === 0 ? (
              <p className="text-secondary mb-0">Nenhuma venda para este cliente.</p>
            ) : (
              <Table size="sm" responsive className="mb-0">
                <thead>
                  <tr>
                    <th>Venda</th>
                    <th>Data</th>
                    <th>Pagamento</th>
                    <th className="text-end">Total</th>
                    <th>Situação</th>
                  </tr>
                </thead>
                <tbody>
                  {sales.map((sale) => (
                    <tr key={sale.id}>
                      <td>#{sale.id}</td>
                      <td>{formatDateTime(sale.saleDate)}</td>
                      <td>{paymentMethodLabels[sale.paymentMethod]}</td>
                      <td className="text-end">{formatCurrency(sale.total)}</td>
                      <td>
                        <SaleStatusBadge status={sale.status} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </>
        )}
          </Tab>
          <Tab eventKey="loyalty" title="Pontos de fidelidade">
            <CustomerLoyaltyPanel customer={customer} />
          </Tab>
        </Tabs>
      </Modal.Body>
      <Modal.Footer>
        <Button variant="outline-secondary" onClick={onClose}>
          Fechar
        </Button>
      </Modal.Footer>
    </Modal>
  )
}
