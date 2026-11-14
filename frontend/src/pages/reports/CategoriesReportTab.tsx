import { useState } from 'react'
import { Card, Col, Row, Table } from 'react-bootstrap'
import { reportsApi } from '../../api/reports'
import { RankingChart } from '../../components/charts/RankingChart'
import { EmptyState } from '../../components/EmptyState'
import { PeriodFilter } from '../../components/PeriodFilter'
import { useReport } from '../../hooks/useReport'
import type { Period } from '../../types/report'
import { formatCurrency, formatPercent } from '../../utils/format'
import { presetPeriod } from '../../utils/period'
import { ReportState } from './ReportState'

export function CategoriesReportTab() {
  const [period, setPeriod] = useState<Period>(() => presetPeriod('30d'))
  const report = useReport(() => reportsApi.categories(period), [period])

  return (
    <>
      <div className="mb-3">
        <PeriodFilter value={period} onChange={setPeriod} />
      </div>
      <ReportState {...report}>
        {(data) =>
          data.items.length === 0 ? (
            <EmptyState title="Nenhuma venda no período" />
          ) : (
            <Row className="g-3">
              <Col lg={5}>
                <Card>
                  <Card.Body>
                    <RankingChart
                      valueLabel="Faturamento"
                      formatValue={formatCurrency}
                      items={data.items.map((c) => ({
                        label: c.categoryName,
                        value: c.revenue,
                        details: [{ label: 'Participação', value: formatPercent(c.percent) }],
                      }))}
                    />
                  </Card.Body>
                </Card>
              </Col>
              <Col lg={7}>
                <Card>
                  <Table responsive hover size="sm" className="mb-0 align-middle">
                    <thead>
                      <tr>
                        <th className="ps-3">Categoria</th>
                        <th className="text-end">Unidades</th>
                        <th className="text-end">Faturamento</th>
                        <th className="text-end">Lucro</th>
                        <th className="text-end pe-3">Participação</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.items.map((c) => (
                        <tr key={c.categoryId}>
                          <td className="ps-3">{c.categoryName}</td>
                          <td className="text-end">{c.quantity}</td>
                          <td className="text-end">{formatCurrency(c.revenue)}</td>
                          <td className="text-end">{formatCurrency(c.profit)}</td>
                          <td className="text-end pe-3">{formatPercent(c.percent)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                </Card>
              </Col>
            </Row>
          )
        }
      </ReportState>
    </>
  )
}
