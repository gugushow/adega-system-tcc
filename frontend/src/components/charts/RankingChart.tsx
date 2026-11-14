import { Bar, BarChart, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { ChartTooltip, type TooltipRow } from './ChartTooltip'
import { axisTick, chartColors, horizontalBarRadius } from './chartTheme'

export interface RankingItem {
  label: string
  value: number
  /** Linhas extras do tooltip (ex.: faturamento e lucro de um produto). */
  details?: TooltipRow[]
}

interface RankingChartProps {
  items: RankingItem[]
  /** Como mostrar o valor (ex.: "12 un." ou "R$ 1.234,00"). */
  formatValue: (value: number) => string
  valueLabel: string
}

/**
 * Ranking em barras horizontais (mais vendidos, categorias, formas de pagamento).
 * Barras deitadas deixam os nomes legíveis por inteiro, o que um gráfico de pizza não faz.
 */
export function RankingChart({ items, formatValue, valueLabel }: RankingChartProps) {
  const height = items.length * 40 + 8

  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={items} layout="vertical" margin={{ top: 4, right: 96, bottom: 4, left: 0 }} barCategoryGap="25%">
        <XAxis type="number" hide />
        <YAxis
          type="category"
          dataKey="label"
          width={150}
          tick={axisTick}
          tickLine={false}
          axisLine={false}
          tickFormatter={(value: string) => (value.length > 22 ? `${value.slice(0, 21)}…` : value)}
        />
        <Tooltip
          cursor={{ fill: chartColors.cursor }}
          content={({ active, payload }) => {
            const item = payload?.[0]?.payload as RankingItem | undefined
            if (!active || !item) return null
            return (
              <ChartTooltip
                title={item.label}
                rows={[{ label: valueLabel, value: formatValue(item.value), color: chartColors.series1 }, ...(item.details ?? [])]}
              />
            )
          }}
        />
        <Bar dataKey="value" fill={chartColors.series1} radius={horizontalBarRadius} maxBarSize={22}>
          <LabelList
            dataKey="value"
            position="right"
            formatter={(value) => formatValue(Number(value))}
            style={{ fill: chartColors.text, fontSize: 12 }}
          />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  )
}
