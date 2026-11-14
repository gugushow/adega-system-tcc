import type { ReactNode } from 'react'
import { Card } from 'react-bootstrap'
import { Link } from 'react-router'

/** Tom do card: dá a cor discreta do ícone e da faixa lateral. "neutral" é o padrão. */
export type StatTone = 'neutral' | 'primary' | 'info' | 'success' | 'warning' | 'danger'

interface StatCardProps {
  label: string
  value: string
  /** Texto pequeno embaixo do número (ex.: "em até 30 dias"). */
  hint?: ReactNode
  /** Classe do Bootstrap Icons (ex.: "bi-cash-coin"). O ícone é decorativo: o rótulo já diz o que é. */
  icon?: string
  tone?: StatTone
  /** Quando há algo a resolver: mostra o aviso (ícone + texto, não só cor) e o link para a tela certa. */
  alert?: { text: string; to: string }
}

/** Card com um número em destaque, usado no dashboard e nos resumos dos relatórios. */
export function StatCard({ label, value, hint, icon, tone = 'neutral', alert }: StatCardProps) {
  return (
    <Card className={`stat-card h-100 stat-${tone}${alert ? ' stat-attention' : ''}`}>
      <Card.Body className="py-3">
        <div className="d-flex align-items-start justify-content-between gap-2">
          <div className="text-secondary small">{label}</div>
          {icon && (
            <span className="stat-icon" aria-hidden="true">
              <i className={`bi ${icon}`} />
            </span>
          )}
        </div>
        <div className="stat-value">{value}</div>
        {hint && <div className="text-secondary small">{hint}</div>}
        {alert && (
          <Link to={alert.to} className="stat-alert small">
            <i className="bi bi-exclamation-triangle me-1" aria-hidden="true" />
            {alert.text}
            <i className="bi bi-arrow-right ms-1" aria-hidden="true" />
          </Link>
        )}
      </Card.Body>
    </Card>
  )
}
