import { useState } from 'react'
import { Card, Col, Row, Table } from 'react-bootstrap'
import { reportsApi } from '../../api/reports'
import { RankingChart } from '../../components/charts/RankingChart'
import { EmptyState } from '../../components/EmptyState'
import { PeriodFilter } from '../../components/PeriodFilter'
import { useReport } from '../../hooks/useReport'
import type { Period } from '../../types/report'
import { formatCurrency, formatDate, formatNumber, formatPercent } from '../../utils/format'
import { presetPeriod } from '../../utils/period'
import { ReportState } from './ReportState'

/** Quanto foi comprado de cada fornecedor no período (pela data da nota). */
export function PurchasesReportTab() {
  const [period, setPeriod] = useState<Period>(() => presetPeriod('30d'))
  const report = useReport(() => reportsApi.purchasesBySupplier(period), [period])

  return (
    <>
      <div className="mb-3">
        <PeriodFilter value={period} onChange={setPeriod} />
      </div>
      <ReportState {...report}>
        {(data) =>
          data.items.length === 0 ? (
            <EmptyState title="Nenhuma compra no período" />
          ) : (
            <Row className="g-3">
              <Col lg={5}>
                <Card>
                  <Card.Body>
                    <RankingChart
                      valueLabel="Total comprado"
                      formatValue={formatCurrency}
                      items={data.items.map((s) => ({
                        label: s.supplierName,
                        value: s.totalAmount,
                        details: [
                          { label: 'Compras', value: formatNumber(s.purchaseCount) },
                          { label: 'Participação', value: formatPercent(s.percent) },
                        ],
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
                        <th className="ps-3">Fornecedor</th>
                        <th className="text-end">Compras</th>
                        <th className="text-end">Total</th>
                        <th className="text-end">Participação</th>
                        <th className="pe-3">Última compra</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.items.map((s) => (
                        <tr key={s.supplierId}>
                          <td className="ps-3">{s.supplierName}</td>
                          <td className="text-end">{s.purchaseCount}</td>
                          <td className="text-end">{formatCurrency(s.totalAmount)}</td>
                          <td className="text-end">{formatPercent(s.percent)}</td>
                          <td className="pe-3">{formatDate(s.lastPurchaseDate)}</td>
                        </tr>
                      ))}
                    </tbody>
                    <tfoot>
                      <tr className="fw-semibold">
                        <td className="ps-3">Total</td>
                        <td className="text-end">{data.items.reduce((sum, s) => sum + s.purchaseCount, 0)}</td>
                        <td className="text-end">{formatCurrency(data.items.reduce((sum, s) => sum + s.totalAmount, 0))}</td>
                        <td colSpan={2} />
                      </tr>
                    </tfoot>
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
