import { useState } from 'react'
import { Card, Col, Form, Row, Table } from 'react-bootstrap'
import { reportsApi } from '../../api/reports'
import { RevenueProfitChart } from '../../components/charts/SalesCharts'
import { PeriodFilter } from '../../components/PeriodFilter'
import { StatCard } from '../../components/StatCard'
import { useReport } from '../../hooks/useReport'
import type { Period, ReportGroupBy } from '../../types/report'
import { paymentMethodLabels } from '../../types/sale'
import { formatCurrency, formatDate, formatMonth, formatNumber, formatPercent } from '../../utils/format'
import { presetPeriod } from '../../utils/period'
import { ReportState } from './ReportState'

/** Vendas, faturamento, CMV, lucro e margem do período, com gráfico e tabela por dia ou mês. */
export function SalesReportTab() {
  const [period, setPeriod] = useState<Period>(() => presetPeriod('30d'))
  const [groupBy, setGroupBy] = useState<ReportGroupBy>('Day')
  const report = useReport(() => reportsApi.sales(period, groupBy), [period, groupBy])

  return (
    <>
      <Row className="g-2 align-items-end mb-3">
        <Col xs="auto">
          <PeriodFilter value={period} onChange={setPeriod} />
        </Col>
        <Col xs="auto">
          <Form.Select size="sm" aria-label="Agrupar por" value={groupBy} onChange={(e) => setGroupBy(e.target.value as ReportGroupBy)}>
            <option value="Day">Por dia</option>
            <option value="Month">Por mês</option>
          </Form.Select>
        </Col>
      </Row>

      <ReportState {...report}>
        {(data) => (
          <>
            <Row xs={2} md={4} xl={7} className="g-3 mb-3">
              <Col><StatCard label="Vendas" value={formatNumber(data.summary.salesCount)} /></Col>
              <Col><StatCard label="Faturamento" value={formatCurrency(data.summary.revenue)} /></Col>
              <Col><StatCard label="Descontos" value={formatCurrency(data.summary.discounts)} /></Col>
              <Col><StatCard label="Custo (CMV)" value={formatCurrency(data.summary.cost)} /></Col>
              <Col><StatCard label="Lucro" value={formatCurrency(data.summary.profit)} /></Col>
              <Col><StatCard label="Ticket médio" value={formatCurrency(data.summary.averageTicket)} /></Col>
              <Col><StatCard label="Margem média" value={formatPercent(data.summary.marginPercent)} /></Col>
            </Row>

            <Card className="mb-3">
              <Card.Header className="fw-semibold">Faturamento e lucro</Card.Header>
              <Card.Body>
                <RevenueProfitChart data={data.periods} groupBy={data.groupBy} />
              </Card.Body>
            </Card>

            <Row className="g-3">
              <Col lg={7}>
                <Card>
                  <Card.Header className="fw-semibold">
                    {data.groupBy === 'Month' ? 'Por mês' : 'Por dia'}
                    {/* A tabela lista só os períodos com venda; o gráfico acima continua mostrando todos. */}
                    <small className="text-secondary fw-normal"> · só {data.groupBy === 'Month' ? 'meses' : 'dias'} com venda</small>
                  </Card.Header>
                  <Table responsive hover size="sm" className="mb-0 align-middle">
                    <thead>
                      <tr>
                        <th className="ps-3">{data.groupBy === 'Month' ? 'Mês' : 'Dia'}</th>
                        <th className="text-end">Vendas</th>
                        <th className="text-end">Faturamento</th>
                        <th className="text-end">Custo</th>
                        <th className="text-end">Lucro</th>
                        <th className="text-end pe-3">Margem</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.periods.every((p) => p.salesCount === 0) && (
                        <tr>
                          <td colSpan={6} className="text-center text-secondary py-3">Sem vendas no período.</td>
                        </tr>
                      )}
                      {data.periods.filter((p) => p.salesCount > 0).map((p) => (
                        <tr key={p.period}>
                          <td className="ps-3">{data.groupBy === 'Month' ? formatMonth(p.period) : formatDate(p.period)}</td>
                          <td className="text-end">{formatNumber(p.salesCount)}</td>
                          <td className="text-end">{formatCurrency(p.revenue)}</td>
                          <td className="text-end">{formatCurrency(p.cost)}</td>
                          <td className="text-end">{formatCurrency(p.profit)}</td>
                          <td className="text-end pe-3">{formatPercent(p.marginPercent)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                </Card>
              </Col>
              <Col lg={5}>
                <Card>
                  <Card.Header className="fw-semibold">Formas de pagamento</Card.Header>
                  <Table responsive size="sm" className="mb-0 align-middle">
                    <thead>
                      <tr>
                        <th className="ps-3">Forma</th>
                        <th className="text-end">Vendas</th>
                        <th className="text-end">Faturamento</th>
                        <th className="text-end pe-3">%</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.paymentMethods.length === 0 && (
                        <tr>
                          <td colSpan={4} className="text-center text-secondary py-3">Sem vendas no período.</td>
                        </tr>
                      )}
                      {data.paymentMethods.map((p) => (
                        <tr key={p.paymentMethod}>
                          <td className="ps-3">{paymentMethodLabels[p.paymentMethod]}</td>
                          <td className="text-end">{p.salesCount}</td>
                          <td className="text-end">{formatCurrency(p.revenue)}</td>
                          <td className="text-end pe-3">{formatPercent(p.percent)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                </Card>
              </Col>
            </Row>
          </>
        )}
      </ReportState>
    </>
  )
}
