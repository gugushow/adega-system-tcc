import { useEffect, useState, type ReactNode } from 'react'
import { Button, ButtonGroup, Card, Col, Row } from 'react-bootstrap'
import { Link } from 'react-router'
import { getErrorMessage } from '../../api/http'
import { dashboardApi } from '../../api/reports'
import { RankingChart } from '../../components/charts/RankingChart'
import { RevenueChart, RevenueProfitChart } from '../../components/charts/SalesCharts'
import { QuickProductLookup } from '../../components/barcode/QuickProductLookup'
import { EmptyState } from '../../components/EmptyState'
import { ErrorState } from '../../components/ErrorState'
import { Loading } from '../../components/Loading'
import { StatCard } from '../../components/StatCard'
import type { Dashboard } from '../../types/report'
import { paymentMethodLabels } from '../../types/sale'
import { formatCurrency, formatNumber, formatPercent } from '../../utils/format'


function ChartCard({ title, action, children }: { title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <Card className="h-100">
      <Card.Header className="d-flex justify-content-between align-items-center">
        <span className="fw-semibold">{title}</span>
        {action}
      </Card.Header>
      <Card.Body>{children}</Card.Body>
    </Card>
  )
}

/** A tela de Lotes tem abas de 7, 30 e 60 dias: abre a mais próxima do prazo usado no card. */
const expiringLink = (days: number) => `/lotes?visao=expiring${days <= 7 ? 7 : days <= 30 ? 30 : 60}`

/**
 * Tela inicial do ADMIN: os números do dia e do mês, a situação do estoque e os gráficos.
 * Tudo vem de uma requisição só (GET /api/dashboard).
 */
export function DashboardPage() {
  const [data, setData] = useState<Dashboard | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [days, setDays] = useState<7 | 30>(7)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let ignore = false
    dashboardApi
      .get()
      .then((result) => !ignore && setData(result))
      .catch((err) => !ignore && setError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [reloadKey])

  function retry() {
    setError(null)
    setReloadKey((key) => key + 1)
  }

  if (error) return <ErrorState message={error} onRetry={retry} />
  if (!data) return <Loading />

  const { cards } = data
  const dailySales = data.last30Days.slice(-days)
  const noSales = (
    <EmptyState icon="bi-bar-chart" title="Sem vendas no período" description="O gráfico aparece assim que houver vendas." />
  )

  return (
    <>
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <h1 className="h4 mb-0">Dashboard</h1>
        <div className="d-flex flex-wrap align-items-center gap-2">
          <Link to="/relatorios" className="btn btn-outline-secondary">
            <i className="bi bi-bar-chart-line me-1" aria-hidden="true" />
            Ver relatórios
          </Link>
          <QuickProductLookup />
        </div>
      </div>

      <h2 className="section-title">Vendas</h2>
      <Row xs={2} lg={4} className="g-3 mb-4">
        <Col><StatCard label="Vendas hoje" value={formatNumber(cards.salesToday)} icon="bi-receipt" tone="primary" /></Col>
        <Col><StatCard label="Faturamento hoje" value={formatCurrency(cards.revenueToday)} icon="bi-cash-coin" tone="info" /></Col>
        <Col><StatCard label="Faturamento do mês" value={formatCurrency(cards.revenueMonth)} icon="bi-calendar3" tone="info" /></Col>
        <Col><StatCard label="Lucro do mês" value={formatCurrency(cards.profitMonth)} icon="bi-graph-up-arrow" tone="success" /></Col>
      </Row>

      <h2 className="section-title">Estoque</h2>
      <Row xs={2} md={3} xl={6} className="g-3 mb-4">
        <Col><StatCard label="Produtos cadastrados" value={formatNumber(cards.activeProducts)} hint="ativos" icon="bi-box-seam" /></Col>
        <Col><StatCard label="Itens em estoque" value={formatNumber(cards.totalItemsInStock)} hint="unidades" icon="bi-boxes" /></Col>
        <Col>
          <StatCard
            label="Estoque baixo"
            value={formatNumber(cards.lowStockProducts)}
            icon="bi-arrow-down-circle"
            tone={cards.lowStockProducts > 0 ? 'warning' : 'neutral'}
            hint="no mínimo ou abaixo"
            alert={cards.lowStockProducts > 0 ? { text: 'Ver produtos', to: '/produtos?estoque=Low' } : undefined}
          />
        </Col>
        <Col>
          <StatCard
            label="Sem estoque"
            value={formatNumber(cards.outOfStockProducts)}
            icon="bi-x-circle"
            tone={cards.outOfStockProducts > 0 ? 'danger' : 'neutral'}
            alert={cards.outOfStockProducts > 0 ? { text: 'Ver produtos', to: '/produtos?estoque=Out' } : undefined}
          />
        </Col>
        <Col>
          <StatCard
            label="Próximos do vencimento"
            value={formatNumber(cards.expiringProducts)}
            icon="bi-hourglass-split"
            tone={cards.expiringProducts > 0 ? 'warning' : 'neutral'}
            hint={`em até ${cards.expiringDays} dias`}
            alert={cards.expiringProducts > 0 ? { text: 'Ver lotes', to: expiringLink(cards.expiringDays) } : undefined}
          />
        </Col>
        <Col>
          <StatCard
            label="Vencidos"
            value={formatNumber(cards.expiredProducts)}
            icon="bi-calendar-x"
            tone={cards.expiredProducts > 0 ? 'danger' : 'neutral'}
            hint="com saldo no estoque"
            alert={cards.expiredProducts > 0 ? { text: 'Ver lotes', to: '/lotes?visao=expired' } : undefined}
          />
        </Col>
      </Row>

      <Row className="g-3 mb-3">
        <Col lg={6}>
          <ChartCard
            title={`Vendas dos últimos ${days} dias`}
            action={
              <ButtonGroup size="sm" aria-label="Período do gráfico">
                <Button variant={days === 7 ? 'secondary' : 'outline-secondary'} onClick={() => setDays(7)}>
                  7 dias
                </Button>
                <Button variant={days === 30 ? 'secondary' : 'outline-secondary'} onClick={() => setDays(30)}>
                  30 dias
                </Button>
              </ButtonGroup>
            }
          >
            <RevenueChart data={dailySales} groupBy="Day" />
          </ChartCard>
        </Col>
        <Col lg={6}>
          <ChartCard title="Faturamento e lucro mensal">
            <RevenueProfitChart data={data.last12Months} groupBy="Month" />
          </ChartCard>
        </Col>
      </Row>

      <Row className="g-3">
        <Col lg={4}>
          <ChartCard title="Mais vendidos (30 dias)">
            {data.topProducts.length === 0
              ? noSales
              : (
                <RankingChart
                  valueLabel="Quantidade"
                  formatValue={(v) => `${formatNumber(v)} un.`}
                  items={data.topProducts.map((p) => ({
                    label: p.productName,
                    value: p.quantity,
                    details: [
                      { label: 'Faturamento', value: formatCurrency(p.revenue) },
                      { label: 'Lucro', value: formatCurrency(p.profit) },
                    ],
                  }))}
                />
              )}
          </ChartCard>
        </Col>
        <Col lg={4}>
          <ChartCard title="Categorias mais vendidas (30 dias)">
            {data.topCategories.length === 0
              ? noSales
              : (
                <RankingChart
                  valueLabel="Faturamento"
                  formatValue={formatCurrency}
                  items={data.topCategories.map((c) => ({
                    label: c.categoryName,
                    value: c.revenue,
                    details: [
                      { label: 'Participação', value: formatPercent(c.percent) },
                      { label: 'Unidades', value: formatNumber(c.quantity) },
                    ],
                  }))}
                />
              )}
          </ChartCard>
        </Col>
        <Col lg={4}>
          <ChartCard title="Formas de pagamento (mês)">
            {data.paymentMethodsMonth.length === 0
              ? noSales
              : (
                <RankingChart
                  valueLabel="Faturamento"
                  formatValue={formatCurrency}
                  items={data.paymentMethodsMonth.map((p) => ({
                    label: paymentMethodLabels[p.paymentMethod],
                    value: p.revenue,
                    details: [
                      { label: 'Participação', value: formatPercent(p.percent) },
                      { label: 'Vendas', value: formatNumber(p.salesCount) },
                    ],
                  }))}
                />
              )}
          </ChartCard>
        </Col>
      </Row>
    </>
  )
}
