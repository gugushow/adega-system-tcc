import { Card, Col, Row, Table } from 'react-bootstrap'
import { reportsApi } from '../../api/reports'
import { StatCard } from '../../components/StatCard'
import { useReport } from '../../hooks/useReport'
import { formatCurrency, formatNumber } from '../../utils/format'
import { ReportState } from './ReportState'

/** Foto do estoque agora: unidades e valor (a custo e a preço de venda) por categoria. */
export function StockReportTab() {
  const report = useReport(() => reportsApi.stock(), [])

  return (
    <ReportState {...report}>
      {(data) => (
        <>
          <Row xs={2} md={4} className="g-3 mb-3">
            <Col><StatCard label="Produtos ativos" value={formatNumber(data.productCount)} /></Col>
            <Col><StatCard label="Unidades em estoque" value={formatNumber(data.units)} /></Col>
            <Col><StatCard label="Valor a custo" value={formatCurrency(data.costValue)} hint="O que foi investido" /></Col>
            <Col><StatCard label="Valor a preço de venda" value={formatCurrency(data.saleValue)} hint="Se tudo fosse vendido" /></Col>
          </Row>
          <Card>
            <Card.Header className="fw-semibold">Por categoria</Card.Header>
            <Table responsive hover size="sm" className="mb-0 align-middle">
              <thead>
                <tr>
                  <th className="ps-3">Categoria</th>
                  <th className="text-end">Produtos</th>
                  <th className="text-end">Unidades</th>
                  <th className="text-end">Valor a custo</th>
                  <th className="text-end pe-3">Valor a preço de venda</th>
                </tr>
              </thead>
              <tbody>
                {data.categories.map((c) => (
                  <tr key={c.categoryId}>
                    <td className="ps-3">{c.categoryName}</td>
                    <td className="text-end">{c.productCount}</td>
                    <td className="text-end">{c.units}</td>
                    <td className="text-end">{formatCurrency(c.costValue)}</td>
                    <td className="text-end pe-3">{formatCurrency(c.saleValue)}</td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </Card>
        </>
      )}
    </ReportState>
  )
}
