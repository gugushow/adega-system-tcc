import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { ReportGroupBy, SalesPeriod } from '../../types/report'
import { formatCurrency, formatCurrencyCompact, formatDate, formatDayMonth, formatMonth, formatNumber, formatPercent } from '../../utils/format'
import { ChartTooltip } from './ChartTooltip'
import { axisTick, chartColors, verticalBarRadius } from './chartTheme'

interface SalesChartProps {
  data: SalesPeriod[]
  groupBy: ReportGroupBy
  height?: number
}

const periodLabel = (period: string, groupBy: ReportGroupBy) =>
  groupBy === 'Month' ? formatMonth(period) : formatDayMonth(period)

const periodTitle = (period: string, groupBy: ReportGroupBy) =>
  groupBy === 'Month' ? formatMonth(period) : formatDate(period)

/** Faturamento por dia (ou mês), uma barra por período. O tooltip mostra também vendas e lucro. */
export function RevenueChart({ data, groupBy, height = 260 }: SalesChartProps) {
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }} barCategoryGap="20%">
        <CartesianGrid vertical={false} stroke={chartColors.grid} />
        <XAxis
          dataKey="period"
          tickFormatter={(value: string) => periodLabel(value, groupBy)}
          tick={axisTick}
          tickLine={false}
          axisLine={{ stroke: chartColors.grid }}
          minTickGap={12}
        />
        <YAxis tickFormatter={formatCurrencyCompact} tick={axisTick} tickLine={false} axisLine={false} width={72} />
        <Tooltip
          cursor={{ fill: chartColors.cursor }}
          content={({ active, payload }) => {
            const row = payload?.[0]?.payload as SalesPeriod | undefined
            if (!active || !row) return null
            return (
              <ChartTooltip
                title={periodTitle(row.period, groupBy)}
                rows={[
                  { label: 'Faturamento', value: formatCurrency(row.revenue), color: chartColors.series1 },
                  { label: 'Vendas', value: formatNumber(row.salesCount) },
                  { label: 'Lucro', value: formatCurrency(row.profit) },
                ]}
              />
            )
          }}
        />
        <Bar dataKey="revenue" name="Faturamento" fill={chartColors.series1} radius={verticalBarRadius} maxBarSize={40} />
      </BarChart>
    </ResponsiveContainer>
  )
}

/**
 * Faturamento e lucro lado a lado. Os dois estão em reais, então dividem o mesmo eixo
 * (nunca dois eixos Y diferentes num gráfico só: confunde a leitura).
 */
export function RevenueProfitChart({ data, groupBy, height = 280 }: SalesChartProps) {
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }} barCategoryGap="20%" barGap={2}>
        <CartesianGrid vertical={false} stroke={chartColors.grid} />
        <XAxis
          dataKey="period"
          tickFormatter={(value: string) => periodLabel(value, groupBy)}
          tick={axisTick}
          tickLine={false}
          axisLine={{ stroke: chartColors.grid }}
          minTickGap={8}
        />
        <YAxis tickFormatter={formatCurrencyCompact} tick={axisTick} tickLine={false} axisLine={false} width={72} />
        <Legend
          verticalAlign="top"
          align="right"
          iconType="square"
          iconSize={10}
          height={28}
          formatter={(value: string) => <span style={{ color: chartColors.axisText }}>{value}</span>}
        />
        <Tooltip
          cursor={{ fill: chartColors.cursor }}
          content={({ active, payload }) => {
            const row = payload?.[0]?.payload as SalesPeriod | undefined
            if (!active || !row) return null
            return (
              <ChartTooltip
                title={periodTitle(row.period, groupBy)}
                rows={[
                  { label: 'Faturamento', value: formatCurrency(row.revenue), color: chartColors.series1 },
                  { label: 'Lucro', value: formatCurrency(row.profit), color: chartColors.series2 },
                  { label: 'Margem', value: formatPercent(row.marginPercent) },
                  { label: 'Vendas', value: formatNumber(row.salesCount) },
                ]}
              />
            )
          }}
        />
        <Bar dataKey="revenue" name="Faturamento" fill={chartColors.series1} radius={verticalBarRadius} maxBarSize={32} />
        <Bar dataKey="profit" name="Lucro" fill={chartColors.series2} radius={verticalBarRadius} maxBarSize={32} />
      </BarChart>
    </ResponsiveContainer>
  )
}
