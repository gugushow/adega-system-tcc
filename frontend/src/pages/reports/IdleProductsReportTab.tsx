import { useState } from 'react'
import { Card, Col, Form, Row, Table } from 'react-bootstrap'
import { reportsApi } from '../../api/reports'
import { EmptyState } from '../../components/EmptyState'
import { StatCard } from '../../components/StatCard'
import { useReport } from '../../hooks/useReport'
import { formatCurrency, formatDateTime, formatNumber } from '../../utils/format'
import { ReportState } from './ReportState'

const dayOptions = [30, 60, 90]

/** Produtos com estoque que não vendem há X dias: dinheiro parado na prateleira. */
export function IdleProductsReportTab() {
  const [days, setDays] = useState(30)
  const report = useReport(() => reportsApi.idleProducts(days), [days])

  return (
    <>
      <Row className="g-2 align-items-center mb-3">
        <Col xs="auto">
          <Form.Label htmlFor="idle-days" className="mb-0">Sem venda há pelo menos</Form.Label>
        </Col>
        <Col xs="auto">
          <Form.Select size="sm" id="idle-days" value={days} onChange={(e) => setDays(Number(e.target.value))}>
            {dayOptions.map((d) => (
              <option key={d} value={d}>{d} dias</option>
            ))}
          </Form.Select>
        </Col>
      </Row>

      <ReportState {...report}>
        {(items) =>
          items.length === 0 ? (
            <EmptyState title="Nenhum produto parado" description={`Todos os produtos com estoque venderam nos últimos ${days} dias.`} />
          ) : (
            <>
              <Row xs={1} sm={2} md={3} className="g-3 mb-3">
                <Col><StatCard label="Produtos parados" value={formatNumber(items.length)} /></Col>
                <Col>
                  <StatCard
                    label="Valor parado (a custo)"
                    value={formatCurrency(items.reduce((sum, p) => sum + p.stockCostValue, 0))}
                    hint="Estoque × custo dos produtos da lista"
                  />
                </Col>
              </Row>
              <Card>
                <Table responsive hover size="sm" className="mb-0 align-middle">
                  <thead>
                    <tr>
                      <th className="ps-3">Produto</th>
                      <th>Categoria</th>
                      <th className="text-end">Estoque</th>
                      <th className="text-end">Valor parado</th>
                      <th>Última venda</th>
                      <th className="text-end pe-3">Dias sem venda</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((p) => (
                      <tr key={p.productId}>
                        <td className="ps-3">{p.productName}</td>
                        <td>{p.categoryName}</td>
                        <td className="text-end">{p.currentStock}</td>
                        <td className="text-end">{formatCurrency(p.stockCostValue)}</td>
                        <td>{p.lastSaleDate ? formatDateTime(p.lastSaleDate) : <span className="text-secondary">Nunca vendido</span>}</td>
                        <td className="text-end pe-3">{p.daysWithoutSale}</td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </Card>
            </>
          )
        }
      </ReportState>
    </>
  )
}
