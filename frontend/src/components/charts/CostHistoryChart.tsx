import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { CostHistoryEntry } from '../../types/stock'
import { formatDate, formatDayMonth, formatPercent, formatUnitCost } from '../../utils/format'
import { ChartTooltip } from './ChartTooltip'
import { axisTick, chartColors } from './chartTheme'


/**
 * Evolução do custo de compra: um ponto por compra, na ordem em que aconteceram.
 * O eixo X usa a posição da compra (e não a data) porque duas compras podem cair no mesmo dia.
 * O eixo Y não começa no zero de propósito: o que interessa aqui é ver a variação.
 */
export default function CostHistoryChart({ entries, height = 220 }: { entries: CostHistoryEntry[]; height?: number }) {
  const data = entries.map((entry, index) => ({ ...entry, index }))

  return (
    <ResponsiveContainer width="100%" height={height}>
      <LineChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
        <CartesianGrid vertical={false} stroke={chartColors.grid} />
        <XAxis
          dataKey="index"
          tickFormatter={(index: number) => formatDayMonth(data[index]?.date ?? '')}
          tick={axisTick}
          tickLine={false}
          axisLine={{ stroke: chartColors.grid }}
          minTickGap={12}
        />
        <YAxis
          domain={['auto', 'auto']}
          tickFormatter={(value: number) => formatUnitCost(value)}
          tick={axisTick}
          tickLine={false}
          axisLine={false}
          width={80}
        />
        <Tooltip
          cursor={{ stroke: chartColors.grid }}
          content={({ active, payload }) => {
            const row = payload?.[0]?.payload as (CostHistoryEntry & { index: number }) | undefined
            if (!active || !row) return null
            return (
              <ChartTooltip
                title={`${formatDate(row.date)} · compra #${row.purchaseId}`}
                rows={[
                  { label: 'Custo unitário', value: formatUnitCost(row.costPrice), color: chartColors.series1 },
                  { label: 'Fornecedor', value: row.supplierName },
                  ...(row.changePercent !== null
                    ? [{ label: 'Variação', value: `${row.changePercent > 0 ? '+' : ''}${formatPercent(row.changePercent)}` }]
                    : []),
                ]}
              />
            )
          }}
        />
        <Line
          type="linear"
          dataKey="costPrice"
          name="Custo unitário"
          stroke={chartColors.series1}
          strokeWidth={2}
          dot={{ r: 4, fill: chartColors.series1, stroke: chartColors.surface, strokeWidth: 2 }}
          activeDot={{ r: 6, stroke: chartColors.surface, strokeWidth: 2 }}
        />
      </LineChart>
    </ResponsiveContainer>
  )
}
