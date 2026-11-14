import type { ReactNode } from 'react'

export interface TooltipRow {
  label: string
  value: string
  /** Quadradinho com a cor da série (o texto continua em cinza/preto). */
  color?: string
}

/** Caixinha que aparece ao passar o mouse num gráfico. */
export function ChartTooltip({ title, rows }: { title: ReactNode; rows: TooltipRow[] }) {
  return (
    <div className="chart-tooltip px-3 py-2 small">
      <div className="fw-semibold mb-1">{title}</div>
      {rows.map((row) => (
        <div key={row.label} className="d-flex align-items-center gap-2">
          {row.color && (
            <span className="d-inline-block rounded-1" style={{ width: 10, height: 10, backgroundColor: row.color }} />
          )}
          <span className="text-secondary">{row.label}</span>
          <span className="ms-auto ps-3 fw-medium">{row.value}</span>
        </div>
      ))}
    </div>
  )
}
