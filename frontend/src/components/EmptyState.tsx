import type { ReactNode } from 'react'

interface EmptyStateProps {
  title: string
  description?: ReactNode
  action?: ReactNode
  /** Classe do Bootstrap Icons. Padrão: caixa vazia. */
  icon?: string
}

/** Mensagem exibida quando uma listagem não tem resultados (e nas telas de aviso, como "não encontrada"). */
export function EmptyState({ title, description, action, icon = 'bi-inbox' }: EmptyStateProps) {
  return (
    <div className="state-message">
      <span className="state-icon" aria-hidden="true">
        <i className={`bi ${icon}`} />
      </span>
      <h2 className="h6 mb-1">{title}</h2>
      {description && <p className="text-secondary mb-3">{description}</p>}
      {action}
    </div>
  )
}
