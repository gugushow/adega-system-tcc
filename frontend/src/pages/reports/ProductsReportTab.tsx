import { useState } from 'react'
import { Button, ButtonGroup, Card, Col, Row, Table } from 'react-bootstrap'
import { reportsApi } from '../../api/reports'
import { RankingChart } from '../../components/charts/RankingChart'
import { EmptyState } from '../../components/EmptyState'
import { PeriodFilter } from '../../components/PeriodFilter'
import { useReport } from '../../hooks/useReport'
import type { Period, ProductRankingOrder } from '../../types/report'
import { formatCurrency, formatPercent } from '../../utils/format'
import { presetPeriod } from '../../utils/period'
import { ReportState } from './ReportState'

/** Produtos mais vendidos e menos vendidos: quantidade, faturamento, custo, lucro e margem. */
export function ProductsReportTab() {
  const [period, setPeriod] = useState<Period>(() => presetPeriod('30d'))
  const [order, setOrder] = useState<ProductRankingOrder>('MostSold')
  const report = useReport(() => reportsApi.products(period, order), [period, order])

  return (
    <>
      <Row className="g-2 align-items-end mb-3">
        <Col xs="auto">
          <PeriodFilter value={period} onChange={setPeriod} />
        </Col>
        <Col xs="auto">
          <ButtonGroup aria-label="Ordem">
            <Button variant={order === 'MostSold' ? 'secondary' : 'outline-secondary'} onClick={() => setOrder('MostSold')}>
              Mais vendidos
            </Button>
            <Button variant={order === 'LeastSold' ? 'secondary' : 'outline-secondary'} onClick={() => setOrder('LeastSold')}>
              Menos vendidos
            </Button>
          </ButtonGroup>
        </Col>
      </Row>

      <ReportState {...report}>
        {(data) =>
          data.items.length === 0 ? (
            <EmptyState title="Nenhuma venda no período" description="Produtos que não venderam nada aparecem na aba Parados." />
          ) : (
            <Row className="g-3">
              <Col xl={4}>
                <Card>
                  <Card.Header className="fw-semibold">Top 10 por quantidade</Card.Header>
                  <Card.Body>
                    <RankingChart
                      valueLabel="Quantidade"
                      formatValue={(v) => `${v} un.`}
                      items={data.items.slice(0, 10).map((p) => ({
                        label: p.productName,
                        value: p.quantity,
                        details: [{ label: 'Faturamento', value: formatCurrency(p.revenue) }],
                      }))}
                    />
                  </Card.Body>
                </Card>
              </Col>
              <Col xl={8}>
                <Card>
                  <Table responsive hover size="sm" className="mb-0 align-middle">
                    <thead>
                      <tr>
                        <th className="ps-3">#</th>
                        <th>Produto</th>
                        <th className="text-end">Qtd.</th>
                        <th className="text-end">Faturamento</th>
                        <th className="text-end">Custo</th>
                        <th className="text-end">Lucro</th>
                        <th className="text-end pe-3">Margem</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.items.map((p, index) => (
                        <tr key={p.productId}>
                          <td className="ps-3 text-secondary">{index + 1}</td>
                          <td>
                            <div className="fw-medium">{p.productName}</div>
                            <small className="text-secondary">{p.categoryName}</small>
                          </td>
                          <td className="text-end">{p.quantity}</td>
                          <td className="text-end">{formatCurrency(p.revenue)}</td>
                          <td className="text-end">{formatCurrency(p.cost)}</td>
                          <td className="text-end">{formatCurrency(p.profit)}</td>
                          <td className="text-end pe-3">{formatPercent(p.marginPercent)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                </Card>
                <small className="text-secondary">
                  O faturamento de cada produto já desconta a parte dele no desconto da venda.
                </small>
              </Col>
            </Row>
          )
        }
      </ReportState>
    </>
  )
}
